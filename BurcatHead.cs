using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol
{
    public record BurcatHead(Guid StreamID, BurcatHeaderSet Headers);
    public sealed record BurcatRelayHead(BurcatHeaderSet AdditionalHeaders): BurcatHead(Guid.Empty, [..BurcatChat.Headers, ..AdditionalHeaders]);
    public sealed record BurcatSendHead(IdentifiedStream Stream, BurcatHeaderSet AdditionalHeaders) : BurcatHead(Stream.Identifier, [.. BurcatChat.Headers, .. AdditionalHeaders]);
}
