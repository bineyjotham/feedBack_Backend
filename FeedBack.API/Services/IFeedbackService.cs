using FeedBack.API.Dtos;

namespace FeedBack.API.Services;

public interface IFeedbackService
{
    Task<FeedbackResponseDto> SubmitFeedbackAsync(FeedbackRequestDto request, string ipAddress, string userAgent);
}