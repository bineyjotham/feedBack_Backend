using Prometheus;

namespace FeedBack.API.Services;

public class MetricsService : IMetricsService
{
    // Counter metrics
    private static readonly Counter FeedbackSubmittedTotal = Metrics
        .CreateCounter("feedback_submitted_total", "Total number of feedback submissions",
            new CounterConfiguration
            {
                LabelNames = new[] { "source", "rating_range" }
            });

    private static readonly Counter EmailsSentTotal = Metrics
        .CreateCounter("emails_sent_total", "Total number of emails sent",
            new CounterConfiguration
            {
                LabelNames = new[] { "type", "status" }
            });

    private static readonly Counter ApiRequestsTotal = Metrics
        .CreateCounter("api_requests_total", "Total API requests",
            new CounterConfiguration
            {
                LabelNames = new[] { "endpoint", "method", "status_code" }
            });

    private static readonly Counter RateLimitHitsTotal = Metrics
        .CreateCounter("ratelimit_hits_total", "Total rate limit hits",
            new CounterConfiguration
            {
                LabelNames = new[] { "endpoint", "client_ip" }
            });

    // Histogram metrics
    private static readonly Histogram ApiRequestDuration = Metrics
        .CreateHistogram("api_request_duration_milliseconds", "API request duration in milliseconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "endpoint", "method" },
                Buckets = new[] { 10.0, 50.0, 100.0, 200.0, 500.0, 1000.0, 2000.0, 5175.0 }
            });

    private static readonly Histogram DatabaseQueryDuration = Metrics
        .CreateHistogram("database_query_duration_milliseconds", "Database query duration in milliseconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "operation" },
                Buckets = new[] { 1.0, 5.0, 10.0, 25.0, 50.0, 100.0, 250.0, 500.0, 1000.0 }
            });

    private static readonly Histogram EmailQueueDuration = Metrics
        .CreateHistogram("email_queue_processing_duration_seconds", "Email queue processing duration in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "status" }
            });

    // Gauge metrics
    private static readonly Gauge ActiveUsers = Metrics
        .CreateGauge("active_users_total", "Total number of currently active users");

    private static readonly Gauge EmailQueueSize = Metrics
        .CreateGauge("email_queue_size", "Current size of email queue");

    private static readonly Gauge DatabaseConnectionPool = Metrics
        .CreateGauge("database_connection_pool_size", "Database connection pool size");

    // Summary metrics
    // private static readonly Summary FeedbackRatingSummary = Metrics
    //     .CreateSummary("feedback_rating_summary", "Summary of feedback ratings",
    //         new SummaryConfiguration
    //         {
    //             LabelNames = new[] { "source" },
    //             Objectives = new[] { 0.5, 0.9, 0.95, 0.99 }
    //         });

    private static readonly Summary FeedbackRatingSummary = Metrics
    .CreateSummary("feedback_rating_summary", "Summary of feedback ratings",
        new SummaryConfiguration
        {
            LabelNames = new[] { "source" },
            Objectives = new List<Prometheus.QuantileEpsilonPair>
            {
                new Prometheus.QuantileEpsilonPair(0.5, 0.001),
                new Prometheus.QuantileEpsilonPair(0.9, 0.001),
                new Prometheus.QuantileEpsilonPair(0.95, 0.001),
                new Prometheus.QuantileEpsilonPair(0.99, 0.001)
            }
        });

    public void RecordFeedbackSubmitted(string source, int rating)
    {
        var ratingRange = rating switch
        {
            >= 9 => "9-10",
            >= 7 => "7-8",
            >= 5 => "5-6",
            >= 3 => "3-4",
            _ => "0-2"
        };
        
        FeedbackSubmittedTotal.WithLabels(source, ratingRange).Inc();
        FeedbackRatingSummary.WithLabels(source).Observe(rating);
    }

    public void RecordEmailSent(string type, bool success)
    {
        EmailsSentTotal.WithLabels(type, success ? "success" : "failed").Inc();
    }

    public void RecordApiRequest(string endpoint, string method, int statusCode, double durationMs)
    {
        ApiRequestsTotal.WithLabels(endpoint, method, statusCode.ToString()).Inc();
        ApiRequestDuration.WithLabels(endpoint, method).Observe(durationMs);
    }

    public void RecordDatabaseQuery(string operation, double durationMs)
    {
        DatabaseQueryDuration.WithLabels(operation).Observe(durationMs);
    }

    public void RecordRateLimitHit(string endpoint, string clientIp)
    {
        RateLimitHitsTotal.WithLabels(endpoint, clientIp).Inc();
    }

    public void IncrementActiveUsers()
    {
        ActiveUsers.Inc();
    }

    public void DecrementActiveUsers()
    {
        ActiveUsers.Dec();
    }

    public void SetCurrentEmailQueueSize(int size)
    {
        EmailQueueSize.Set(size);
    }

    public void SetDatabaseConnectionPoolSize(int size)
    {
        DatabaseConnectionPool.Set(size);
    }
}