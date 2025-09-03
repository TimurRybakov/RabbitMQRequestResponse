using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQRequestResponse.Insfrastructure;

public sealed class RequestSender
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingRequests = new ConcurrentDictionary<string, TaskCompletionSource<string>>();
    private readonly string _replyQueueName;
    private readonly IConnectionPool _connectionPool;

    public RequestSender(IConnectionPool connectionPool)
    {
        _connectionPool = connectionPool;

        // Создаем очередь для ответов
        var channel = _connectionPool.GetChannelAsync(default).Result;
        channel.channel.QueueDeclareAsync

        _replyQueueName = channel.QueueDeclare().QueueName;

        // Подписываемся на получение ответов
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnResponseReceivedAsync;
        _channel.BasicConsume(consumer, _replyQueueName, autoAck: true);
    }

    private Task OnResponseReceivedAsync(object sender, BasicDeliverEventArgs e)
    {
        var correlationId = e.BasicProperties.CorrelationId;

        if (correlationId is null)
            return Task.CompletedTask;

        var response = Encoding.UTF8.GetString(e.Body.ToArray());

        if (_pendingRequests.TryRemove(correlationId, out var tcs))
        {
            tcs.SetResult(response); // Устанавливаем результат для ожидающего запроса
        }

        return Task.CompletedTask;
    }

    public async Task<string> SendRequestAsync(string message, TimeSpan timeout)
    {
        var correlationId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<string>();

        // Добавляем в словарь ожидающий запрос
        _pendingRequests[correlationId] = tcs;

        try
        {
            var props = _channel.CreateBasicProperties();
            props.ReplyTo = _replyQueueName;
            props.CorrelationId = correlationId;

            // Отправляем сообщение
            var body = System.Text.Encoding.UTF8.GetBytes(message);
            _channel.BasicPublish(exchange: "", routingKey: "request_queue", basicProperties: props, body: body);

            // Ожидаем ответ с таймаутом
            using (var cts = new CancellationTokenSource(timeout))
            {
                cts.Token.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);

                return await tcs.Task; // Ждем ответа
            }
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex); // Устанавливаем исключение в случае ошибки
            throw;
        }
        finally
        {
            // Удаляем из словаря после завершения
            _pendingRequests.TryRemove(correlationId, out _);
        }
    }
}
