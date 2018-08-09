using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace VotingService
{
    public class HtmlMediaFormatter : TextOutputFormatter
    {
        public HtmlMediaFormatter()
        {
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("text/html"));
            SupportedEncodings.Add(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        public override Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            using (var writer = new StreamWriter(context.HttpContext.Response.Body, selectedEncoding))
            {
                writer.Write(context.Object);
            }

            return Task.CompletedTask;
        }

        protected override bool CanWriteType(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            // only one ViewModel type is allowed
            return (typeof(string) == type) ? true : false;
        }
    }
}
