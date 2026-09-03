using System.Text.Json.Serialization;
using static Template.Common.Models.ResponseEnums;

namespace Template.Common.Models
{
    public class ApiResponse<T>
    {
        [JsonPropertyName("result")]
        public T? Result { get; set; }
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonIgnore]
        public ResponseEnums.ResponseCodes Code { get; set; }
        [JsonPropertyName("errors")]
        public IEnumerable<string> Errors { get; set; } = new List<string>();
    }

    public static class ResponseMessage
    {
        public static ApiResponse<T> Success<T>(T data, string? message = null)
        {
            return new ApiResponse<T>
            {

                Code = ResponseEnums.ResponseCodes.Success,
                Message = message,
                Result = data
            };
        }

        public static ApiResponse<T> Error<T>(
            T result,
            string? error = null,
            IEnumerable<string>? errors = null,
            ResponseEnums.ResponseCodes responseCodes = ResponseEnums.ResponseCodes.Fail)
        {
            return new ApiResponse<T>
            {

                Code = responseCodes,
                Message = error ?? "An Error Occurred,Please try again",
                Result = result,
                Errors = errors ?? []

            };
        }
    }
}

