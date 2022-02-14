using System;

namespace YtStream.Accounts
{

    /// <summary>
    /// User roles
    /// </summary>
    [Flags]
    public enum UserRoles : int
    {
        User = 1,
        Administrator = 2
    }
}
