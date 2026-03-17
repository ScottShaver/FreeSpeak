namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Defines the common reasons for reporting content in a group.
    /// Users select one of these reasons when submitting a report.
    /// </summary>
    public enum ReportReason
    {
        /// <summary>
        /// The content violates a specific group rule.
        /// When selected, the user should specify which rule was violated.
        /// </summary>
        ViolatesGroupRule = 0,

        /// <summary>
        /// The content contains spam or unwanted commercial content.
        /// </summary>
        Spam = 1,

        /// <summary>
        /// The content contains harassment, bullying, or targeted attacks against individuals.
        /// </summary>
        HarassmentOrBullying = 2,

        /// <summary>
        /// The content contains hate speech targeting race, religion, gender, sexuality, or other protected characteristics.
        /// </summary>
        HateSpeech = 3,

        /// <summary>
        /// The content contains misinformation or false information.
        /// </summary>
        Misinformation = 4,

        /// <summary>
        /// The content contains nudity or sexual content.
        /// </summary>
        AdultContent = 5,

        /// <summary>
        /// The content contains violent or graphic imagery or promotes violence.
        /// </summary>
        ViolenceOrGraphicContent = 6,

        /// <summary>
        /// The content infringes on intellectual property rights (copyright, trademark).
        /// </summary>
        IntellectualPropertyViolation = 7,

        /// <summary>
        /// The content is not relevant to the group's topic or purpose.
        /// </summary>
        OffTopic = 8,

        /// <summary>
        /// The content contains personal information about someone without their consent.
        /// </summary>
        PrivacyViolation = 9,

        /// <summary>
        /// The content promotes or glorifies self-harm or suicide.
        /// </summary>
        SelfHarmOrSuicide = 10,

        /// <summary>
        /// The content promotes illegal activities or substances.
        /// </summary>
        IllegalActivity = 11,

        /// <summary>
        /// Other reason not covered by the predefined options.
        /// The user should provide additional details in the description.
        /// </summary>
        Other = 99
    }
}
