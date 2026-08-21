namespace BurcatProtocol.Transactions
{
    /// <summary>
    /// Provides the transactional entry points for Burcat protocol operations.
    /// </summary>
    /// <remarks>
    /// Transaction handling is not implemented yet. Operations currently delegate to
    /// <see cref="BurcatChat" /> while preserving a single canonical overload for each
    /// operation family.
    /// </remarks>
    public static class BurcatTransactionalChat
    {
        /// <summary>Relays an add or update operation through the configured providers.</summary>
        public static Task<BurcatException?> RelayCoupleAsync(BurcatInstance instance, bool ignoreInternal = false, CancellationToken? token = null) =>
            BurcatChat.RelayCoupleAsync(instance, ignoreInternal, token);

        /// <summary>Relays an add or update operation through the configured providers.</summary>
        public static Task<BurcatException?> RelayCoupleAsync<T>(T objectBDP, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject =>
            RelayCoupleAsync(BurcatInstance.Build(objectBDP), ignoreInternal, token);

        /// <summary>Relays an add or update operation through the configured providers.</summary>
        public static BurcatException? RelayCouple(BurcatInstance instance, bool ignoreInternal = false, CancellationToken? token = null) =>
            RelayCoupleAsync(instance, ignoreInternal, token).GetAwaiter().GetResult();

        /// <summary>Relays an add or update operation through the configured providers.</summary>
        public static BurcatException? RelayCouple<T>(T objectBDP, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject =>
            RelayCouple(BurcatInstance.Build(objectBDP), ignoreInternal, token);

        /// <summary>Sends an add or update request through a stream.</summary>
        public static Task<BurcatException?> SendCoupleAsync(IdentifiedStream stream, BurcatInstance instance, CancellationToken? token = null) =>
            BurcatChat.SendCoupleAsync(stream, instance, token);

        /// <summary>Sends an add or update request through a stream.</summary>
        public static Task<BurcatException?> SendCoupleAsync<T>(IdentifiedStream stream, T objectBDP, CancellationToken? token = null) where T : IBurcatObject =>
            SendCoupleAsync(stream, BurcatInstance.Build(objectBDP), token);

        /// <summary>Sends an add or update request through a stream.</summary>
        public static BurcatException? SendCouple(IdentifiedStream stream, BurcatInstance instance, CancellationToken? token = null) =>
            SendCoupleAsync(stream, instance, token).GetAwaiter().GetResult();

        /// <summary>Sends an add or update request through a stream.</summary>
        public static BurcatException? SendCouple<T>(IdentifiedStream stream, T objectBDP, CancellationToken? token = null) where T : IBurcatObject =>
            SendCouple(stream, BurcatInstance.Build(objectBDP), token);

        /// <summary>Relays a delete operation through the configured providers.</summary>
        public static Task<BurcatException?> RelayDecoupleAsync(BurcatInstance instance, bool ignoreInternal = false, CancellationToken? token = null) =>
            BurcatChat.RelayDecoupleAsync(instance, ignoreInternal, token);

        /// <summary>Relays a delete operation through the configured providers.</summary>
        public static Task<BurcatException?> RelayDecoupleAsync<T>(T objectBDP, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject =>
            RelayDecoupleAsync(BurcatInstance.Build(objectBDP), ignoreInternal, token);

        /// <summary>Relays a delete operation through the configured providers.</summary>
        public static BurcatException? RelayDecouple(BurcatInstance instance, bool ignoreInternal = false, CancellationToken? token = null) =>
            RelayDecoupleAsync(instance, ignoreInternal, token).GetAwaiter().GetResult();

        /// <summary>Relays a delete operation through the configured providers.</summary>
        public static BurcatException? RelayDecouple<T>(T objectBDP, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject =>
            RelayDecouple(BurcatInstance.Build(objectBDP), ignoreInternal, token);

        /// <summary>Sends a delete request through a stream.</summary>
        public static Task<BurcatException?> SendDecoupleAsync(IdentifiedStream stream, BurcatInstance instance, CancellationToken? token = null) =>
            BurcatChat.SendDecoupleAsync(stream, instance, token);

        /// <summary>Sends a delete request through a stream.</summary>
        public static Task<BurcatException?> SendDecoupleAsync<T>(IdentifiedStream stream, T objectBDP, CancellationToken? token = null) where T : IBurcatObject =>
            SendDecoupleAsync(stream, BurcatInstance.Build(objectBDP), token);

        /// <summary>Sends a delete request through a stream.</summary>
        public static BurcatException? SendDecouple(IdentifiedStream stream, BurcatInstance instance, CancellationToken? token = null) =>
            SendDecoupleAsync(stream, instance, token).GetAwaiter().GetResult();

        /// <summary>Sends a delete request through a stream.</summary>
        public static BurcatException? SendDecouple<T>(IdentifiedStream stream, T objectBDP, CancellationToken? token = null) where T : IBurcatObject =>
            SendDecouple(stream, BurcatInstance.Build(objectBDP), token);

        /// <summary>Relays an action through the configured providers.</summary>
        public static Task<ActionResult> RelayActionAsync(BurcatInstance instance, string action, object?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) =>
            BurcatChat.RelayActionAsync(instance, action, parameters, ignoreInternal, token);

        /// <summary>Relays an instance action through the configured providers.</summary>
        public static Task<ActionResult> RelayActionAsync<T>(T objectBDP, string action, object?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject =>
            RelayActionAsync(new BurcatInstance(objectBDP), action, parameters, ignoreInternal, token);

        /// <summary>Relays a type-level action through the configured providers.</summary>
        public static Task<ActionResult> RelayActionAsync<T>(string action, object?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject =>
            RelayActionAsync(BurcatInstance.Build<T>(), action, parameters, ignoreInternal, token);

        /// <summary>Relays an action through the configured providers.</summary>
        public static ActionResult RelayAction(BurcatInstance instance, string action, object?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) =>
            RelayActionAsync(instance, action, parameters, ignoreInternal, token).GetAwaiter().GetResult();

        /// <summary>Relays an instance action through the configured providers.</summary>
        public static ActionResult RelayAction<T>(T objectBDP, string action, object?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject =>
            RelayAction(new BurcatInstance(objectBDP), action, parameters, ignoreInternal, token);

        /// <summary>Relays a type-level action through the configured providers.</summary>
        public static ActionResult RelayAction<T>(string action, object?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject =>
            RelayAction(BurcatInstance.Build<T>(), action, parameters, ignoreInternal, token);

        /// <summary>Sends an action request through a stream.</summary>
        public static Task<ActionResult> SendActionAsync(IdentifiedStream stream, BurcatInstance instance, string action, object?[]? parameters = null, CancellationToken? token = null) =>
            BurcatChat.SendActionAsync(stream, instance, action, parameters, token);

        /// <summary>Sends an instance action request through a stream.</summary>
        public static Task<ActionResult> SendActionAsync<T>(IdentifiedStream stream, T objectBDP, string action, object?[]? parameters = null, CancellationToken? token = null) where T : IBurcatObject =>
            SendActionAsync(stream, new BurcatInstance(objectBDP), action, parameters, token);

        /// <summary>Sends a type-level action request through a stream.</summary>
        public static Task<ActionResult> SendActionAsync<T>(IdentifiedStream stream, string action, object?[]? parameters = null, CancellationToken? token = null) where T : IBurcatObject =>
            SendActionAsync(stream, BurcatInstance.Build<T>(), action, parameters, token);

        /// <summary>Sends an action request through a stream.</summary>
        public static ActionResult SendAction(IdentifiedStream stream, BurcatInstance instance, string action, object?[]? parameters = null, CancellationToken? token = null) =>
            SendActionAsync(stream, instance, action, parameters, token).GetAwaiter().GetResult();

        /// <summary>Sends an instance action request through a stream.</summary>
        public static ActionResult SendAction<T>(IdentifiedStream stream, T objectBDP, string action, object?[]? parameters = null, CancellationToken? token = null) where T : IBurcatObject =>
            SendAction(stream, new BurcatInstance(objectBDP), action, parameters, token);

        /// <summary>Sends a type-level action request through a stream.</summary>
        public static ActionResult SendAction<T>(IdentifiedStream stream, string action, object?[]? parameters = null, CancellationToken? token = null) where T : IBurcatObject =>
            SendAction(stream, BurcatInstance.Build<T>(), action, parameters, token);

        /// <summary>Receives and processes the next protocol exchange from a stream.</summary>
        public static Task<ExchangeResult> ReceiveAsync(IdentifiedStream stream, CancellationToken? token = null) =>
            BurcatChat.ReceiveAsync(stream, token);

        /// <summary>Receives and processes the next protocol exchange from a stream.</summary>
        public static ExchangeResult Receive(IdentifiedStream stream, CancellationToken? token = null) =>
            ReceiveAsync(stream, token).GetAwaiter().GetResult();
    }
}
