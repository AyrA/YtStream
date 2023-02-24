namespace YtStream.Interfaces
{
    /// <summary>
    /// Provides basic validation
    /// </summary>
    public interface IValidateable
    {
        /// <summary>
        /// Checks if this instance is in a valid state
        /// </summary>
        /// <returns>true if valid state, false otherwise</returns>
        /// <remarks>
        /// This call should be synonymous with <see cref="GetValidationMessages"/> resulting in zero messages
        /// </remarks>
        bool IsValid();

        /// <summary>
        /// Gets all messages that explain failed validation
        /// </summary>
        /// <returns>Array with messages</returns>
        /// <remarks>
        /// If no messages returned, then the instance is valid.
        /// See also <see cref="IsValid"/>
        /// </remarks>
        string[] GetValidationMessages();
    }
}
