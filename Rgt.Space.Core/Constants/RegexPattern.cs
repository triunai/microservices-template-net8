using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rgt.Space.Core.Constants
{
    public static class RegexPattern
    {
        public const string Phone = @"^[0-9+]+[0-9+\-\s]*[0-9]+$";
        public const string Email = @"^[\w!#$%&'*+\-/=?\^_`{|}~]+(\.[\w!#$%&'*+\-/=?\^_`{|}~]+)*@((([\-\w]+\.)+[a-zA-Z]{2,4})|(([0-9]{1,3}\.){3}[0-9]{1,3}))$";
        public const string Name = @"^[a-zA-Z]{1,}[a-zA-Z.@/' -]*$";
        public const string ICNumber = @"^(\d{6}-\d{2}-\d{4}|\d{12})$";
        public const string NumericString = @"\b(\d{4,})\b";
        public const string PostalCode = @"^\d{5}$";
    }
    public static class RegexPatternFluentValidation
    {
        public const string Phone = @"^\+?[0-9]{8,15}$";
        public const string Email = @"^[\w!#$%&'*+\-/=?\^_`{|}~]+(\.[\w!#$%&'*+\-/=?\^_`{|}~]+)*@((([\-\w]+\.)+[a-zA-Z]{2,4})|(([0-9]{1,3}\.){3}[0-9]{1,3}))$";
        public const string Name = @"^[A-Z0-9\s'@/\.\-]{1,80}$";
        public const string ICNumber = @"^(\d{6}-\d{2}-\d{4}|\d{12})$";
        public const string NumericString = @"\b(\d{4,})\b";
        public const string PostalCode = @"^[A-Za-z0-9\s\-]{1,10}$";
    }
}
