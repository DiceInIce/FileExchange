namespace FileShareServer.Constants
{
    /// <summary>
    /// Application-wide constants for magic strings
    /// </summary>
    public static class AppConstants
    {
        // CORS
        public const string CorsPolicyName = "AllowAll";

        // Database
        public const string DatabaseUsersTableName = "Users";

        // SignalR Hub Methods
        public static class HubMethods
        {
            public const string SocialDataChanged = "SocialDataChanged";
            public const string FriendRequestReceived = "FriendRequestReceived";
            public const string ReceiveMessage = "ReceiveMessage";
            public const string FileTransferAvailable = "FileTransferAvailable";
        }

        // SignalR Hub Messages
        public static class MessageTypes
        {
            public const string Text = "Text";
            public const string File = "File";
        }

        // File Messages
        public static class FileMessageFormat
        {
            public const string Separator = "|";
            public const string ServerSource = "server";
            public const string P2pSource = "p2p";
            public const string NoToken = "-";
        }

        // API Endpoints
        public static class ApiRoutes
        {
            public const string ApiPrefix = "/api";

            public static class Auth
            {
                public const string Root = "/auth";
                public const string Register = "/register";
                public const string Login = "/login";
            }

            public static class Users
            {
                public const string Root = "/users";
                public const string Search = "/search/{query}";
                public const string ById = "/{id}";
            }

            public static class Friends
            {
                public const string Root = "/friends";
                public const string SendRequest = "/request/{friendId}";
                public const string Accept = "/accept/{friendId}";
                public const string Reject = "/reject/{friendId}";
                public const string Remove = "/remove/{friendId}";
                public const string List = "/list";
                public const string Pending = "/pending";
                public const string Sent = "/sent";
            }

            public static class Chat
            {
                public const string Root = "/chat";
                public const string Conversation = "/conversation/{friendId}";
                public const string Unread = "/unread";
                public const string MarkRead = "/mark-read/{messageId}";
                public const string Store = "/store/{friendId}";
                public const string StoreFile = "/store-file/{friendId}";
            }

            public static class Files
            {
                public const string Root = "/files";
                public const string Upload = "/upload/{receiverId}";
                public const string Inbox = "/inbox";
                public const string Download = "/download/{id}";
            }
        }

        // Error Messages
        public static class ErrorMessages
        {
            public const string UsernameExists = "Username already exists";
            public const string CannotSendFriendRequest = "Cannot send friend request";
            public const string CannotAcceptFriendRequest = "Cannot accept friend request";
            public const string CannotRejectFriendRequest = "Cannot reject friend request";
            public const string CannotRemoveFriend = "Cannot remove friend";
            public const string FileIsEmpty = "Файл пустой.";
            public const string FriendsOnly = "Передача файлов доступна только друзьям.";
            public const string MessageContentRequired = "Message content is required.";
            public const string FileMetadataRequired = "File metadata is required.";
            public const string FileNotFoundInStorage = "Файл не найден в хранилище.";
            public const string FileNotFoundOnDisk = "Файл отсутствует на диске.";
            public const string Unauthorized = "Unauthorized";
        }

        // Success Messages
        public static class SuccessMessages
        {
            public const string FileUploaded = "File uploaded successfully";
        }
    }
}
