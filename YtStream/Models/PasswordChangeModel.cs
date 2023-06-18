namespace YtStream.Models
{
    /// <summary>
    /// Model to implement password changes
    /// </summary>
    public class PasswordChangeModel
    {
        /// <summary>
        /// Current user password
        /// </summary>
        public string? OldPassword { get; set; }

        /// <summary>
        /// New user password
        /// </summary>
        public string? NewPassword { get; set; }

        /// <summary>
        /// Confirmation of new user password
        /// </summary>
        public string? ConfirmPassword { get; set; }
    }
}
