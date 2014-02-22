using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Web.Http;

namespace AsyncEmail.Controllers
{
    public class EmailController : ApiController
    {
        // This takes a JSON array similar to [ "erez@taskmatics.com", "dave@taskmatics.com" ]
        public async Task<object> Post(string[] emails)
        {
            // Create options that allow multiple emails to be sent concurrently.
            var sendOptions = new ExecutionDataflowBlockOptions();
            sendOptions.MaxDegreeOfParallelism = Environment.ProcessorCount;

            // Create a results bag and email client.
            var results = new Dictionary<string, string>();
            var emailClient = CreateEmailClient();

            // Create action block that sends email asynchronously to the server.
            var emailSender = new ActionBlock<string>(new Func<string, Task>(
                async email =>
                {
                    try
                    {
                        // Start sending email and return thread to thread pool.
                        await emailClient.SendMailAsync(
                            "from@domain.com", email, "Test Email", "Test email body.");

                        // Resume on another thread after email sent and set result to 'success'.
                        results[email] = "success";
                    }
                    catch
                    {
                        // Resume on another thread after email sent and set result to 'fail'.
                        results[email] = "fail";
                    }
                }),
                sendOptions);

            // Push email addresses into the action block.
            foreach (var email in emails)
                emailSender.Post(email);

            // Tell the action block to complete and signal completion when data is flushed.
            emailSender.Complete();
            await emailSender.Completion;

            // Return results similar to { "erez@taskmatics.com": "success", "dave@taskmatics.com": "success" }
            return results;
        }

        private SmtpClient CreateEmailClient()
        {
            // Create simulated outbox path and ensure the directory exists.
            var outboxPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Outbox");
            Directory.CreateDirectory(outboxPath);

            // Create an email client that drops an email into the specified directory.
            var emailClient = new SmtpClient();
            emailClient.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            emailClient.PickupDirectoryLocation = outboxPath;

            return emailClient;
        }
    }
}
