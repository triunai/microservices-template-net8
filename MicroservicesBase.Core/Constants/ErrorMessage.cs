﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroservicesBase.Core.Constants
{
    public static class ErrorMessage
    {
        public const string InternalError = "Something went wrong. Please try again later.";
        public const string NotFoundMessage = "The requested resource could not be found.";
        public const string AppConfigurationMessage = "Unable to retrieve application settings.";
        public const string TransactionNotCommit = "The transaction could not be committed.";
        public const string TransactionNotExecute = "The transaction could not be executed.";
    }
}
