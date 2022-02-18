using System;

namespace YtStream.Accounts
{
    /// <summary>
    /// User roles
    /// </summary>
    [Flags]
    public enum UserRoles : int
    {
        /// <summary>
        /// Regular user that can log in and stream content
        /// </summary>
        User = 1,
        /// <summary>
        /// Administrator with management access
        /// </summary>
        Administrator = 2
    }
}
