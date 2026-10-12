namespace HotelResearch.App;

public sealed class HotelResearchResultUnavailableException(string mtHotelCode)
    : Exception($"No readable successful hotel research result exists for MTHotelCode '{mtHotelCode}'.");

public sealed class HotelResearchConfigurationException(string message) : InvalidOperationException(message);

public sealed class HotelResearchExecutionException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
