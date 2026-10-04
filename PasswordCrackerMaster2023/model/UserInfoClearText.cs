using System;

namespace PasswordCrackerMaster2023.model
{
    /// <summary>
    /// Username + password in clear text (that is human readable)
    /// </summary>
   public class UserInfoClearText
    {
        public String UserName { get; set; }
        public String Password { get; set; }

        public UserInfoClearText(string username, string password)
        {
            if (username == null)
            {
                throw new ArgumentNullException("username");
            }
            if (password == null)
            {
                throw new ArgumentNullException("password");
            }
            UserName = username;
            Password = password;
        }

        public override string ToString()
        {
            return UserName + ": " + Password;
        }
    }
}
