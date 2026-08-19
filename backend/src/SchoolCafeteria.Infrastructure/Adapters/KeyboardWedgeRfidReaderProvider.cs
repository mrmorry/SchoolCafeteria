using SchoolCafeteria.Application.Abstractions;

namespace SchoolCafeteria.Infrastructure.Adapters;

/// <summary>
/// Default operating mode: the RFID reader behaves as a USB/Bluetooth keyboard and types the UID
/// directly into the focused POS input field — no server-side device integration exists or is
/// needed for this mode, the UID simply arrives as a normal API request field. This class exists
/// only to satisfy IRfidReaderProvider for future server-pushed integrations (a local agent
/// bridging a serial/WebUSB reader); it intentionally has no reads to offer today.
/// </summary>
public class KeyboardWedgeRfidReaderProvider : IRfidReaderProvider
{
    public string ProviderName => "keyboard-wedge";

    public Task<string?> ReadNextUidAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);
}
