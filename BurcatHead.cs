using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol
{
    public record BurcatHead(Guid StreamID, BurcatHeaderSet Headers);
    public sealed record BurcatBoradcastHead(BurcatHeaderSet AdditionalHeaders): BurcatHead(Guid.Empty, [..BurcatChat.Headers, ..AdditionalHeaders]);
    public sealed record BurcatDirectionalHead(IdentifiedStream Stream, BurcatHeaderSet AdditionalHeaders) : BurcatHead(Stream.Identifier, [.. BurcatChat.Headers, .. AdditionalHeaders]);
}