using Tmds.DBus.Protocol;

namespace LifeCharter.Platform.Common.Business;

/// <summary>Writes the arguments of a D-Bus method call.</summary>
public delegate void MessageBodyWriter(ref MessageWriter writer);
