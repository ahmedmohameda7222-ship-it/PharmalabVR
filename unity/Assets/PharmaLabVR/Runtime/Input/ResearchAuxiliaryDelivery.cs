namespace PharmaLabVR.Input
{
    // Grants only a *request* for a Native command. Actual transfer and receipt
    // come from the existing SessionController/IPhreeqc authority.
    public enum ResearchAuxiliaryDeliveryIntent { None, BottlePour, PipetteDispense, PipetteAspirate }

    // The visible bottle must represent the actual selected Native source.
    // A pipette switched to an unrepresented reserve cannot draw from the
    // original stock bottle merely because its tip is geometrically immersed.
    public static class ResearchPhysicalSourceBinding
    {
        public static bool Matches(string configuredSourceId, string visibleBottleId,
            string nativeSourceId, bool nativeToolExists) =>
            !string.IsNullOrWhiteSpace(configuredSourceId) &&
            !string.IsNullOrWhiteSpace(visibleBottleId) &&
            string.Equals(configuredSourceId, visibleBottleId, System.StringComparison.Ordinal) &&
            (!nativeToolExists || string.Equals(nativeSourceId, visibleBottleId,
                System.StringComparison.Ordinal));
    }

    public static class ResearchAuxiliaryDelivery
    {
        public static ResearchAuxiliaryDeliveryIntent Resolve(
            string physicalToolId, bool directlyHeld, bool deliberatePress,
            bool exclusiveReceiverCapture, bool nativeReady)
        {
            return Resolve(physicalToolId, directlyHeld, deliberatePress,
                exclusiveReceiverCapture, false, nativeReady);
        }

        // Requests must target one physical station. Neither geometry nor
        // presentation authorizes Native material or invents a receipt.
        public static ResearchAuxiliaryDeliveryIntent Resolve(
            string physicalToolId, bool directlyHeld, bool deliberatePress,
            bool exclusiveReceiverCapture, bool immersedInSource, bool nativeReady)
        {
            if (!directlyHeld || !deliberatePress || !nativeReady ||
                exclusiveReceiverCapture == immersedInSource)
                return ResearchAuxiliaryDeliveryIntent.None;
            return physicalToolId switch
            {
                "research-stock-bottle-visual" when exclusiveReceiverCapture =>
                    ResearchAuxiliaryDeliveryIntent.BottlePour,
                "research-pipette-visual" when immersedInSource =>
                    ResearchAuxiliaryDeliveryIntent.PipetteAspirate,
                "research-pipette-visual" when exclusiveReceiverCapture =>
                    ResearchAuxiliaryDeliveryIntent.PipetteDispense,
                _ => ResearchAuxiliaryDeliveryIntent.None
            };
        }
    }
}
