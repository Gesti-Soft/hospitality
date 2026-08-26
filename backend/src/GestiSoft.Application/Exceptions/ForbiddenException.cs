namespace GestiSoft.Application.Exceptions;

public class ForbiddenException(string userMessage) : DomainException(userMessage, statusCode: 403);
