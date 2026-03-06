namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents different types of reactions/likes a user can give to a post
    /// </summary>
    public enum LikeType
    {
        /// <summary>
        /// Default like - thumbs up 👍
        /// </summary>
        Like = 0,

        /// <summary>
        /// Love reaction - heart ❤️
        /// </summary>
        Love = 1,

        /// <summary>
        /// Care reaction - hugging face 🤗
        /// </summary>
        Care = 2,

        /// <summary>
        /// Haha reaction - laughing face 😂
        /// </summary>
        Haha = 3,

        /// <summary>
        /// Wow reaction - surprised face 😮
        /// </summary>
        Wow = 4,

        /// <summary>
        /// Sad reaction - sad face 😢
        /// </summary>
        Sad = 5,

        /// <summary>
        /// Angry reaction - angry face 😠
        /// </summary>
        Angry = 6
    }
}
