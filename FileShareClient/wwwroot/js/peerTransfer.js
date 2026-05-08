window.peerTransfer = (function () {
  const peers = new Map();
  let dotNetRef = null;
  const rtcConfig = { iceServers: [{ urls: "stun:stun.l.google.com:19302" }] };
  const chunkSize = 64 * 1024;

  function ensurePeer(userId, isInitiator) {
    let state = peers.get(userId);
    if (state) return state;

    const pc = new RTCPeerConnection(rtcConfig);
    state = {
      pc,
      userId,
      dc: null,
      recvMeta: null,
      recvChunks: [],
      recvSize: 0,
      remoteSet: false,
      pendingCandidates: []
    };
    peers.set(userId, state);

    pc.onicecandidate = (e) => {
      if (e.candidate && dotNetRef) {
        dotNetRef.invokeMethodAsync("SendIceCandidateToPeer", userId, JSON.stringify(e.candidate));
      }
    };

    if (isInitiator) {
      const dc = pc.createDataChannel("file", { ordered: true });
      setupDataChannel(state, dc);
    } else {
      pc.ondatachannel = (evt) => setupDataChannel(state, evt.channel);
    }

    return state;
  }

  function setupDataChannel(state, dc) {
    state.dc = dc;
    dc.binaryType = "arraybuffer";
    
    dc.onmessage = async (evt) => {
      try {
        const data = evt.data;
        
        // Для текстовых сообщений сохранить JSON
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
            state.recvChunks = [];
            state.recvSize = 0;
            return;
          }
          if (msg.type === "end") {
            const blob = new Blob(state.recvChunks);
            const reader = new FileReader();
            reader.onload = async () => {
              if (dotNetRef) {
                await dotNetRef.invokeMethodAsync("OnPeerTransferStatus", `P2P: файл '${state.recvMeta.name}' получен напрямую`);
                await dotNetRef.invokeMethodAsync(
                  "OnPeerFileReceived",
                  state.userId,
                  state.recvMeta.name,
                  reader.result.split(",")[1], // base64 часть из Data URL
                  state.recvMeta.size,
                  state.recvMeta.token || "-"
                );
              }
            };
            reader.readAsDataURL(blob);
            state.recvMeta = null;
            state.recvChunks = [];
            state.recvSize = 0;
            return;
          }
        }
        
        // Для бинарных chunks - ArrayBuffer
        if (data instanceof ArrayBuffer) {
          state.recvChunks.push(new Uint8Array(data));
          state.recvSize += data.byteLength;
          return;
        }
      } catch {
        // ignore malformed chunk
      }
    };
  }

  async function sendFile(userId, fileName, base64, size) {
    if (!dotNetRef) return { success: false, token: "-" };
    const state = ensurePeer(userId, true);
    const token = (crypto && crypto.randomUUID) ? crypto.randomUUID() : `${Date.now()}_${Math.random().toString(16).slice(2)}`;

    if (!state.remoteSet) {
      const offer = await state.pc.createOffer();
      await state.pc.setLocalDescription(offer);
      await dotNetRef.invokeMethodAsync("SendOfferToPeer", userId, JSON.stringify(offer));
    }

    let tries = 0;
    while ((!state.dc || state.dc.readyState !== "open") && tries < 60) {
      await new Promise((r) => setTimeout(r, 100));
      tries++;
    }
    if (!state.dc || state.dc.readyState !== "open") return { success: false, token };

    // Конвертировать base64 обратно в бинарные данные для оптимальной передачи
    const binaryString = atob(base64);
    const bytes = new Uint8Array(binaryString.length);
    for (let i = 0; i < binaryString.length; i++) {
      bytes[i] = binaryString.charCodeAt(i);
    }

    state.dc.send(JSON.stringify({ type: "meta", name: fileName, size, token }));
    
    // Отправлять бинарные chunks вместо base64
    for (let i = 0; i < bytes.length; i += chunkSize) {
      const chunk = bytes.slice(i, i + chunkSize);
      state.dc.send(chunk.buffer);
    }
    
    state.dc.send(JSON.stringify({ type: "end" }));
    await dotNetRef.invokeMethodAsync("OnPeerTransferStatus", `P2P: файл '${fileName}' отправлен напрямую`);
    return { success: true, token };
  }

  async function sendText(userId, content) {
    if (!dotNetRef) return false;
    const state = ensurePeer(userId, true);

    if (!state.remoteSet) {
      const offer = await state.pc.createOffer();
      await state.pc.setLocalDescription(offer);
      await dotNetRef.invokeMethodAsync("SendOfferToPeer", userId, JSON.stringify(offer));
    }

    let tries = 0;
    while ((!state.dc || state.dc.readyState !== "open") && tries < 40) {
      await new Promise((r) => setTimeout(r, 100));
      tries++;
    }
    if (!state.dc || state.dc.readyState !== "open") return false;

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

  return { init, dispose, sendFile, sendText, onOffer, onAnswer, onIceCandidate };
})();
