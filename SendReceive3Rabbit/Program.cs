using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

internal class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Enter the client name:");
        var clientId = Console.ReadLine();

        Console.WriteLine("Enter your role (student/teacher):");
        var role = Console.ReadLine().ToLower();

        // Hardcoded password for the "teacher" role
        const string teacherPassword = "teacherpass";
        if (role == "teacher")
        {
            Console.WriteLine("Enter the password for the teacher role:");
            var password = Console.ReadLine();
            if (password != teacherPassword)
            {
                Console.WriteLine("Incorrect password. Exiting...");
                return;
            }
        }

        var factory = new ConnectionFactory { HostName = "localhost", DispatchConsumersAsync = true };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        // Declare the fanout exchange
        channel.ExchangeDeclare(exchange: "message_exchange", type: ExchangeType.Fanout);

        // Declare a non-durable, exclusive, auto-delete queue with a generated name
        var queueName = channel.QueueDeclare().QueueName;

        // Bind the queue to the exchange
        channel.QueueBind(queue: queueName,
                          exchange: "message_exchange",
                          routingKey: "");

        // Send a join message to the chat
        var joinMessage = $"{clientId} ({role}) joined the chat";
        var joinMessageBody = Encoding.UTF8.GetBytes(joinMessage);
        channel.BasicPublish(exchange: "message_exchange",
                             routingKey: "",
                             basicProperties: null,
                             body: joinMessageBody);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            // Extract the sender's role from the message
            var senderRole = message.Split(':')[0].Split('(')[1].Split(')')[0];

            // Check if the message should be displayed to the current role
            if (senderRole == role || senderRole == "everyone")
            {
                Console.WriteLine($"Received: {message}");
            }

            await Task.Yield(); // Ensure asynchronous execution
        };
        channel.BasicConsume(queue: queueName,
                             autoAck: true,
                             consumer: consumer);

        Console.WriteLine("Type a message and press Enter to send. Press Ctrl+C to exit.");

        while (true)
        {
            try
            {
                var message = Console.ReadLine();
                if (message == null || message.ToLower() == "exit")
                    break;

                var body = Encoding.UTF8.GetBytes($"{clientId} ({role}):{message}");

                // Publish the message to the fanout exchange
                channel.BasicPublish(exchange: "message_exchange",
                                     routingKey: "",
                                     basicProperties: null,
                                     body: body);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        Console.WriteLine($"Closing {clientId}. Press any key to exit...");
        Console.ReadKey();
    }
}
