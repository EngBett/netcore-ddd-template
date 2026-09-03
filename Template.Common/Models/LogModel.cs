namespace Template.Common.Models
{
    public class LogModel
    {

        public string RequestUrl { get; set; } = string.Empty;
        public object? Request { get; set; }

        public object? Response { get; set; }
        public DateTime RequestTime { get; set; } = DateTime.UtcNow;
        public DateTime ResponseTime { get; set; }
        public string TimeTaken { get; set; } = string.Empty;

        public LogModel() { }
        public LogModel(string requestUrl, object request)
        {
            this.Request =request;
            this.RequestUrl = requestUrl;
            RequestTime = DateTime.UtcNow;
        }
        public void CalculateTime(object? response = null)
        {
            ResponseTime = DateTime.UtcNow;
            TimeSpan timeTaken = ResponseTime - RequestTime;
            TimeTaken = $"{timeTaken.TotalSeconds} Secs";
            Response = response;

        }
    }
}
