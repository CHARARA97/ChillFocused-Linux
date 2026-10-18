# Sync

`HeadlessRunner`（线程循环）、`FocusStatusProbe`、`RulePublisher`、`SessionReporter`、`RuleJson`、`RuleSync`、`HttpTransport`、`FocusedClient`、`FocusedProtocol`。

规则与心跳是独立通道：规则失败不能阻断心跳。只经 `IStatusFeed`/`IOverlayActions` 暴露窄接口。
