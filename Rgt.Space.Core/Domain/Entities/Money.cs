using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rgt.Space.Core.Domain.Entities
{
    public sealed record Money(decimal Amount, string Currency) 
    {
        public static Money Zero(string currency) => new(0m, currency);

        public Money EnsureNonNegative()
            => Amount < 0m ? throw new ArgumentOutOfRangeException(nameof(Amount), "Money cannot be negative.") : this;

        public Money Add(Money other)
        {
            if (!Currency.Equals(other.Currency, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Currency mismatch.");
            return new Money(Amount + other.Amount, Currency);
        }
    }
}
