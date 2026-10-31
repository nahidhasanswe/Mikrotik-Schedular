namespace MikrotikScheduler.Application.Common;

public class NotFoundException(string message) : Exception(message);
public class ConflictException(string message) : Exception(message);
public class UnauthorizedAppException(string message) : Exception(message);
public class RouterConnectionException(string message, Exception? inner = null) : Exception(message, inner);
