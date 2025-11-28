using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Rgt.Space.Core.Utilities
{
    public class DecimalPrecisionConverter(int precision) : JsonConverter<decimal>
    {
        private readonly int _precision = precision;

        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return Math.Round(reader.GetDecimal(), _precision);
        }

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(Math.Round(value, _precision));
        }
    }
}
