using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace Adapt.Lib
{
    public static class Extensions
    {
        public static ILogger Here(this ILogger logger,
                                   [CallerMemberName] string memberName = "",
                                   [CallerFilePath] string sourceFilePath = "",
                                   [CallerLineNumber] int sourceLineNumber = 0)
        {
            return PrepareLogger(logger, memberName, sourceFilePath, sourceLineNumber);
        }

        public static string FirstCharToLowerCase(this string str)
        {
            if (string.IsNullOrEmpty(str) || char.IsLower(str[0]))
            {
                return str;
            }

            return char.ToLower(str[0]) + str[1..];
        }

        public static string SurroundWithCodeBlock(this string input, string syntax = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"```{syntax ?? string.Empty}");
            sb.AppendLine(input);
            sb.AppendLine("```");

            return sb.ToString();
        }

        public static string SeparateByUpperCase(this string input, char separator)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            var sb = new StringBuilder();

            for (var i = 0; i < input.Length; i++)
            {
                var current = input[i];

                if (i == 0)
                {
                    sb.Append(current);
                    continue;
                }

                if (char.IsUpper(current))
                {
                    sb.Append(separator);
                }

                sb.Append(current);
            }

            return sb.ToString();
        }

        public static async Task TryCatch(this Task task,
                                          string errorMessage,
                                          [CallerMemberName] string memberName = "",
                                          [CallerFilePath] string sourceFilePath = "",
                                          [CallerLineNumber] int sourceLineNumber = 0)
        {
            try
            {
                await task;
            }
            catch (Exception e)
            {
                Log.Logger.PrepareLogger(memberName, sourceFilePath, sourceLineNumber).Error(e, errorMessage);
            }
        }

        private static ILogger PrepareLogger(this ILogger logger, string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0)
        {
            string fileName = null;
            if (!string.IsNullOrWhiteSpace(sourceFilePath))
            {
                fileName = Path.GetFileNameWithoutExtension(sourceFilePath);
            }

            return logger
                  .ForContext("MemberName", memberName)
                  .ForContext("FilePath", fileName ?? sourceFilePath)
                  .ForContext("LineNumber", sourceLineNumber);
        }
    }
}