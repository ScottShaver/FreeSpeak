namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Defines the different types of emotional reactions a user can express on posts and comments.
    /// Similar to social media reaction systems, providing richer interaction options beyond a simple like.
    /// </summary>
    public enum LikeType
    {
        /// <summary>
        /// Standard like reaction. Represented by a thumbs up emoji (👍).
        /// Default reaction type.
        /// </summary>
        Like = 0,

        /// <summary>
        /// Love reaction. Represented by a heart emoji (❤️).
        /// Expresses strong positive affection or appreciation.
        /// </summary>
        Love = 1,

        /// <summary>
        /// Care reaction. Represented by a hugging face emoji (🤗).
        /// Expresses empathy, support, or compassion.
        /// </summary>
        Care = 2,

        /// <summary>
        /// Haha reaction. Represented by a laughing face emoji (😂).
        /// Indicates something is funny or amusing.
        /// </summary>
        Haha = 3,

        /// <summary>
        /// Wow reaction. Represented by a surprised face emoji (😮).
        /// Expresses amazement, surprise, or being impressed.
        /// </summary>
        Wow = 4,

        /// <summary>
        /// Sad reaction. Represented by a crying face emoji (😢).
        /// Expresses sympathy, sorrow, or disappointment.
        /// </summary>
        Sad = 5,

        /// <summary>
        /// Angry reaction. Represented by an angry face emoji (😠).
        /// Expresses frustration, anger, or strong disagreement.
        /// </summary>
        Angry = 6
    }
}
