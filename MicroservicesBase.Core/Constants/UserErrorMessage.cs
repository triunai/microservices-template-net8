using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroservicesBase.Core.Constants
{
    public static class UserErrorMessage
    {
        public const string AlreadyExists = "{0} already exists!";
        public const string Unauthorized = "User is not logged in.";
        public const string UserNotExist = "The specified user does not exist.";
        public const string PasswordIncorrect = "The password entered is incorrect.";
    }
}
