namespace Rgt.Space.Core.Constants;

public static class FeatureFlagConstants
{
    public static class ReasonCodes
    {
        public const string FeatureNotFound = "FEATURE_NOT_FOUND";
        public const string GlobalOff = "GLOBAL_OFF";
        public const string RequiresClient = "REQUIRES_CLIENT";
        public const string ClientNotSubscribed = "CLIENT_NOT_SUBSCRIBED";
        public const string ClientOff = "CLIENT_OFF";
        public const string UserForceOff = "USER_FORCE_OFF";
        public const string UserForceOn = "USER_FORCE_ON";
        public const string GrantedSystem = "GRANTED_SYSTEM";
        public const string GrantedClient = "GRANTED_CLIENT";
        public const string InvalidClientId = "INVALID_CLIENT_ID";
    }

    public static class OverrideStates
    {
        public const string ForceOn = "FORCE_ON";
        public const string ForceOff = "FORCE_OFF";

        public static readonly IReadOnlySet<string> All = new HashSet<string> { ForceOn, ForceOff };
    }

    public static class Permissions
    {
        public const string ListView = "FEATURES.LIST.VIEW";
        public const string GlobalEdit = "FEATURES.GLOBAL.EDIT";
        public const string ClientEdit = "FEATURES.CLIENT.EDIT";
        public const string OverrideInsert = "FEATURES.OVERRIDE.INSERT";
        public const string OverrideDelete = "FEATURES.OVERRIDE.DELETE";
        public const string EvaluateView = "FEATURES.EVALUATE.VIEW";
    }

    public const int CacheTtlSeconds = 60;
}
