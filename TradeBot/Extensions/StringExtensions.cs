﻿using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TradeBot.Extensions
{
    public static class StringExtensions
    {
        public static int? ToInt(this string str)
        {
            int result;
            bool validInput = int.TryParse(str, out result);
            return validInput ? result : default(int?);
        }

        public static double? ToDouble(this string str)
        {
            double result;
            bool validInput = double.TryParse(str, out result);
            return validInput ? result : default(double?);
        }

        public static string ToPrettyString(this object obj)
        {
            var keyValuePairs = obj.GetType().GetProperties()
                .Select(p => new KeyValuePair<string, object>(p.Name, p.GetValue(obj, null)));
            return ToPrettyString(keyValuePairs);
        }

        public static string ToPrettyString(this IEnumerable<KeyValuePair<string, object>> keyValuePairs)
        {
            if (keyValuePairs.Count() == 0)
            {
                return "{}";
            }

            string indent = new string(' ', 2);
            StringBuilder builder = new StringBuilder();
            builder.Append("{");
            foreach (var pair in keyValuePairs)
            {
                builder
                    .AppendLine()
                    .Append(indent)
                    .AppendFormat("{0} : {1}", pair.Key, pair.Value);
            }
            builder
                .AppendLine()
                .Append("}");
            return builder.ToString();
        }
    }
}