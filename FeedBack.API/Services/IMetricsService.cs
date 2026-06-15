namespace FeedBack.API.Services;

public interface IMetricsService
{
    void RecordFeedbackSubmitted(string source, int rating);
    void RecordEmailSent(string type, bool success);
    void RecordApiRequest(string endpoint, string method, int statusCode, double durationMs);
    void RecordDatabaseQuery(string operation, double durationMs);
    void RecordRateLimitHit(string endpoint, string clientIp);
    void IncrementActiveUsers();
    void DecrementActiveUsers();
    void SetCurrentEmailQueueSize(int size);
}