using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroservicesBase.Core.Constants
{
    //stored procedures names go here, instead of hardcoding into CQRS/Database access
    public static class StoredProcedureNames
    {
        public const string GetSaleWithItems = "dbo.GetSaleWithItems";


    }
}
