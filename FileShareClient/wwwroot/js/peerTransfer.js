window.peerTransfer = (function () {
  const peers = new Map();
  let dotNetRef = null;
  const rtcConfig = {
    iceServers: [
      { urls: "stun:stun.l.google.com:19302" },
      { urls: "stun:stun1.l.google.com:19302" },
      { urls: "stun:stun2.l.google.com:19302" }
    ]
  };
  const chunkSize = 256 * 1024;
  /** Один вызов dc.send — не больше этого размера (SCTP/WebView2 иначе рвёт канал на больших файлах). */
  const maxSctpPayloadPerSend = 64 * 1024;

  function generateToken() {
    return (crypto && crypto.randomUUID) ? crypto.randomUUID() : `${Date.now()}_${Math.random().toString(16).slice(2)}`;
  }

  /** Ждём, пока SCTP-буфер data channel не освободится (вместо фиксированной паузы на больших файлах). */
  async function waitForSendWindow(dc, maxBuffered) {
    if (!dc || typeof dc.bufferedAmount !== "number") return;
    const limit = maxBuffered > 0 ? maxBuffered : 8 * 1024 * 1024;
    while (dc.bufferedAmount > limit) {
      await new Promise((r) => setTimeout(r, 4));
    }
  }

  function pickMaxBuffered(size) {
    if (size > 100 * 1024 * 1024) return 4 * 1024 * 1024;
    if (size > 20 * 1024 * 1024) return 3 * 1024 * 1024;
    if (size > 5 * 1024 * 1024) return 2 * 1024 * 1024;
    return 1 * 1024 * 1024;
  }

  function createPeerState(userId) {
    const pc = new RTCPeerConnection(rtcConfig);
    const state = {
      pc,
      userId,
      dc: null,
      recvMeta: null,
      recvSize: 0,
      remoteSet: false,
      pendingCandidates: [],
      closed: false,
      activeOutgoingToken: null,
      activeOutgoingSize: 0
    };

    pc.onicecandidate = (e) => {
      if (e.candidate && dotNetRef) {
        dotNetRef.invokeMethodAsync("SendIceCandidateToPeer", userId, JSON.stringify(e.candidate));
      }
    };

    pc.onconnectionstatechange = async () => {
      const cs = pc.connectionState;
      if (!dotNetRef) return;
      if (cs === "failed" || cs === "closed") {
        state.closed = true;
        await dotNetRef.invokeMethodAsync("OnPeerTransferStatus", "P2P: соединение разорвано, будет пересоздано при следующей отправке.");
      }
    };

    pc.ondatachannel = (evt) => setupDataChannel(state, evt.channel);

    return state;
  }

  function ensurePeer(userId, isInitiator) {
    let state = peers.get(userId);
    if (state && (state.closed || state.pc.connectionState === "failed" || state.pc.connectionState === "closed")) {
      try {
        if (state.dc) state.dc.close();
        state.pc.close();
      } catch {}
      peers.delete(userId);
      state = null;
    }

    if (!state) {
      state = createPeerState(userId);
      peers.set(userId, state);
    }

    if (isInitiator && (!state.dc || state.dc.readyState === "closed")) {
      const dc = state.pc.createDataChannel("file", { ordered: true });
      setupDataChannel(state, dc);
    }

    return state;
  }

  function resetPeer(userId) {
    const state = peers.get(userId);
    if (!state) return;
    try {
      if (state.dc) state.dc.close();
      state.pc.close();
    } catch {}
    peers.delete(userId);
  }

  async function startOffer(state, userId) {
    const offer = await state.pc.createOffer();
    await state.pc.setLocalDescription(offer);
    await dotNetRef.invokeMethodAsync("SendOfferToPeer", userId, JSON.stringify(offer));
  }

  async function waitForOpenChannel(state, maxTries) {
    let tries = 0;
    while ((!state.dc || state.dc.readyState !== "open") && tries < maxTries) {
      await new Promise((r) => setTimeout(r, 100));
      tries++;
    }
    return !!state.dc && state.dc.readyState === "open";
  }

  function setupDataChannel(state, dc) {
    state.dc = dc;
    dc.binaryType = "arraybuffer";

    dc.onmessage = async (evt) => {
      try {
        const data = evt.data;

        if (typeof data === "string") {
          const msg = JSON.parse(data);
          if (msg.type === "text") {
            if (dotNetRef) {
              await dotNetRef.invokeMethodAsync("OnPeerTextMessage", state.userId, msg.content || "");
            }
            return;
          }
          if (msg.type === "meta") {
            state.recvMeta = msg;
            state.recvSize = 0;
            state._recvProgLast = 0;
            if (dotNetRef) {
              const sz = typeof msg.size === "number" ? msg.size : 0;
              dotNetRef.invokeMethodAsync("OnP2pReceiveProgress", state.userId, 0, sz, msg.name || "").catch(() => {});
              const tkn = String(msg.token ?? "-").trim();
              await dotNetRef.invokeMethodAsync(
                "OnP2pInboundStart",
                state.userId,
                tkn,
                msg.name || "",
                sz
              );
            }
            return;
          }
          if (msg.type === "end") {
            if (!state.recvMeta) {
              return;
            }

            const recvMeta = state.recvMeta;
            state.recvMeta = null;
            state.recvSize = 0;

            if (dotNetRef) {
              await dotNetRef.invokeMethodAsync("OnPeerTransferStatus", `P2P: файл '${recvMeta.name}' получен напрямую`);
              const tkn = String(recvMeta.token ?? "-").trim();
              await dotNetRef.invokeMethodAsync(
                "OnP2pInboundComplete",
                state.userId,
                tkn,
                recvMeta.name,
                recvMeta.size
              );
            }
            return;
          }
        }

        if (data instanceof ArrayBuffer) {
          if (!state.recvMeta || !dotNetRef) {
            return;
          }
          const u8 = new Uint8Array(data);
          const tkn = String(state.recvMeta.token ?? "-").trim();
          await dotNetRef.invokeMethodAsync("OnP2pInboundChunk", state.userId, tkn, u8);
          state.recvSize += u8.byteLength;
          const total = state.recvMeta.size || 0;
          const now = Date.now();
          if (!state._recvProgLast) state._recvProgLast = 0;
          if (now - state._recvProgLast >= 150 || state.recvSize >= total) {
            state._recvProgLast = now;
            dotNetRef.invokeMethodAsync(
              "OnP2pReceiveProgress",
              state.userId,
              state.recvSize,
              total,
              state.recvMeta.name || ""
            ).catch(() => {});
          }
          return;
        }
      } catch {
        // ignore malformed chunk
      }
    };
  }

  async function openSendChannel(userId, size) {
    const isLargeFile = size > 10 * 1024 * 1024;
    const maxTries = isLargeFile ? 500 : 200;
    const maxAttempts = isLargeFile ? 3 : 2;
    let state = ensurePeer(userId, true);
    let isOpen = false;

    for (let attempt = 0; attempt < maxAttempts; attempt++) {
      if (!state.remoteSet) {
        await startOffer(state, userId);
      }

      isOpen = await waitForOpenChannel(state, maxTries);
      if (isOpen) break;

      if (attempt < maxAttempts - 1) {
        await dotNetRef.invokeMethodAsync("OnPeerTransferStatus", `P2P: попытка ${attempt + 1} не удалась, повторное соединение...`);
        resetPeer(userId);
        state = ensurePeer(userId, true);
      }
    }

    return { state, isOpen };
  }

  async function sendFilePrepare(userId, fileName, size) {
    if (!dotNetRef) return { success: false, token: "-" };
    const token = generateToken();
    dotNetRef.invokeMethodAsync("OnP2pSendProgress", userId, 0, size, "connecting").catch(() => {});

    const { state, isOpen } = await openSendChannel(userId, size);
    if (!isOpen) return { success: false, token };

    const maxBuf = pickMaxBuffered(size);
    await waitForSendWindow(state.dc, maxBuf);
    state.dc.send(JSON.stringify({ type: "meta", name: fileName, size, token }));
    state.activeOutgoingToken = token;
    state.activeOutgoingSize = size;

    dotNetRef.invokeMethodAsync("OnP2pSendProgress", userId, 0, size, "sending").catch(() => {});
    return { success: true, token };
  }

  async function sendFileAppendBuffer(userId, value) {
    const state = peers.get(userId);
    const dc = state?.dc;
    if (!dc || dc.readyState !== "open") {
      throw new Error("P2P data channel не открыт");
    }
    const maxBuf = pickMaxBuffered(state.activeOutgoingSize || 0);

    let u8;
    if (value instanceof Uint8Array) {
      u8 = value;
    } else if (value instanceof ArrayBuffer) {
      u8 = new Uint8Array(value);
    } else {
      u8 = new Uint8Array(value);
    }

    for (let i = 0; i < u8.length; i += maxSctpPayloadPerSend) {
      await waitForSendWindow(dc, maxBuf);
      const end = Math.min(i + maxSctpPayloadPerSend, u8.length);
      const slice = u8.subarray(i, end);
      dc.send(slice);
    }
  }

  async function sendFileFinalize(userId, fileName) {
    if (!dotNetRef) return { success: false, token: "-" };
    const state = peers.get(userId);
    const token = state?.activeOutgoingToken || "-";
    const size = state?.activeOutgoingSize || 0;
    if (!state?.dc || state.dc.readyState !== "open") {
      if (state) {
        state.activeOutgoingToken = null;
        state.activeOutgoingSize = 0;
      }
      return { success: false, token };
    }
    const maxBuf = pickMaxBuffered(size);
    await waitForSendWindow(state.dc, maxBuf);
    state.dc.send(JSON.stringify({ type: "end" }));
    dotNetRef.invokeMethodAsync("OnP2pSendProgress", userId, size, size, "sending").catch(() => {});
    await dotNetRef.invokeMethodAsync("OnPeerTransferStatus", `P2P: файл '${fileName}' отправлен напрямую`);
    state.activeOutgoingToken = null;
    state.activeOutgoingSize = 0;
    return { success: true, token };
  }

  /**
   * Потоковая отправка: .NET передаёт MemoryStream через DotNetStreamReference — без полной копии файла в JS до начала отправки.
   */
  async function sendFileStream(userId, fileName, size, streamRef) {
    if (!dotNetRef) return { success: false, token: "-" };
    const prep = await sendFilePrepare(userId, fileName, size);
    if (!prep.success) return prep;

    let lastProg = 0;
    let sent = 0;

    try {
      if (typeof streamRef.stream !== "function") {
        const ab = await streamRef.arrayBuffer();
        const u8 = new Uint8Array(ab);
        const sendChunk = size > 10 * 1024 * 1024 ? 512 * 1024 : chunkSize;
        for (let i = 0; i < u8.length; i += sendChunk) {
          const chunk = u8.subarray(i, Math.min(i + sendChunk, u8.length));
          await sendFileAppendBuffer(userId, chunk);
          sent += chunk.byteLength;
          const now = Date.now();
          if (now - lastProg >= 150 || sent >= size) {
            lastProg = now;
            dotNetRef.invokeMethodAsync("OnP2pSendProgress", userId, sent, size, "sending").catch(() => {});
          }
        }
        return await sendFileFinalize(userId, fileName);
      }

      const netStream = await streamRef.stream();
      const reader = netStream.getReader();
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        if (value && value.length > 0) {
          await sendFileAppendBuffer(userId, value);
          sent += value.length;
          const now = Date.now();
          if (now - lastProg >= 150 || sent >= size) {
            lastProg = now;
            dotNetRef.invokeMethodAsync("OnP2pSendProgress", userId, sent, size, "sending").catch(() => {});
          }
        }
      }
    } catch {
      const state = peers.get(userId);
      if (state) {
        state.activeOutgoingToken = null;
        state.activeOutgoingSize = 0;
      }
      return { success: false, token: prep.token };
    }

    return await sendFileFinalize(userId, fileName);
  }

  /** Легаси-путь: весь файл уже в памяти как byte[] из .NET. */
  async function sendFile(userId, fileName, fileData, size) {
    if (!dotNetRef) return { success: false, token: "-" };
    const prep = await sendFilePrepare(userId, fileName, size);
    if (!prep.success) return prep;

    const bytes = fileData instanceof Uint8Array ? fileData : new Uint8Array(fileData);
    const sendChunk = size > 10 * 1024 * 1024 ? 512 * 1024 : chunkSize;
    let sent = 0;
    let lastProg = 0;

    for (let i = 0; i < bytes.length; i += sendChunk) {
      const chunk = bytes.subarray(i, Math.min(i + sendChunk, bytes.length));
      await sendFileAppendBuffer(userId, chunk);
      sent += chunk.byteLength;
      const now = Date.now();
      if (now - lastProg >= 150 || sent >= size) {
        lastProg = now;
        dotNetRef.invokeMethodAsync("OnP2pSendProgress", userId, sent, size, "sending").catch(() => {});
      }
    }

    return await sendFileFinalize(userId, fileName);
  }

  async function sendText(userId, content) {
    if (!dotNetRef) return false;
    let state = ensurePeer(userId, true);
    let isOpen = false;

    for (let attempt = 0; attempt < 2; attempt++) {
      if (!state.remoteSet) {
        await startOffer(state, userId);
      }

      isOpen = await waitForOpenChannel(state, 120);
      if (isOpen) break;

      if (attempt === 0) {
        resetPeer(userId);
        state = ensurePeer(userId, true);
      }
    }

    if (!isOpen) return false;

    state.dc.send(JSON.stringify({ type: "text", content }));
    return true;
  }

  async function onOffer(userId, offerJson) {
    if (!dotNetRef) return;
    const state = ensurePeer(userId, false);
    const offer = JSON.parse(offerJson);
    await state.pc.setRemoteDescription(offer);
    state.remoteSet = true;

    for (const c of state.pendingCandidates) {
      await state.pc.addIceCandidate(c);
    }
    state.pendingCandidates = [];

    const answer = await state.pc.createAnswer();
    await state.pc.setLocalDescription(answer);
    await dotNetRef.invokeMethodAsync("SendAnswerToPeer", userId, JSON.stringify(answer));
  }

  async function onAnswer(userId, answerJson) {
    const state = peers.get(userId);
    if (!state) return;
    const answer = JSON.parse(answerJson);
    await state.pc.setRemoteDescription(answer);
    state.remoteSet = true;

    for (const c of state.pendingCandidates) {
      await state.pc.addIceCandidate(c);
    }
    state.pendingCandidates = [];
  }

  async function onIceCandidate(userId, candidateJson) {
    const state = ensurePeer(userId, false);
    const candidate = new RTCIceCandidate(JSON.parse(candidateJson));
    if (state.remoteSet) {
      await state.pc.addIceCandidate(candidate);
    } else {
      state.pendingCandidates.push(candidate);
    }
  }

  function init(ref) {
    dotNetRef = ref;
  }

  function dispose() {
    for (const [, state] of peers) {
      try {
        if (state.dc) state.dc.close();
        state.pc.close();
      } catch {}
    }
    peers.clear();
    dotNetRef = null;
  }

  return { init, dispose, sendFile, sendFileStream, sendText, onOffer, onAnswer, onIceCandidate };
})();
