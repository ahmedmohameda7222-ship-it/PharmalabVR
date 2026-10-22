namespace PharmaLabVR.Input
{
    // Grants only a *request* for a Native command. Actual transfer and receipt
    // come from the existing SessionController/IPhreeqc authority.
    public enum ResearchAuxiliaryDeliveryIntent { None, BottlePour, PipetteDispense, PipetteAspirate, BurettePrime, PipetteSelectPrimary, PipetteSelectReserve }

    // The visible bottle must represent the actual selected Native source.
    // Only the Native tool's currently assigned source (not the prefab default)
    // may admit physical immersion after an explicit reserve switch.
    public static class ResearchPhysicalSourceBinding
    {
        public static bool Matches(string configuredSourceId, string visibleBottleId,
            string nativeSourceId, bool nativeToolExists) =>
            !string.IsNullOrWhiteSpace(configuredSourceId) &&
            !string.IsNullOrWhiteSpace(visibleBottleId) &&
            string.Equals(nativeToolExists ? nativeSourceId : configuredSourceId,
                visibleBottleId, System.StringComparison.Ordinal);
    }

    // A deliberate press while immersed in a DIFFERENT prepared stock asks
    // only to change the pipette's Native source. Student must press again to
    // aspirate, so no unobserved mixing/volume changes can be hidden by UI.
    public static class ResearchPhysicalSourceSelection
    {
        public static ResearchAuxiliaryDeliveryIntent Resolve(
            string toolId, bool directlyHeld, bool deliberatePress,
            bool receiverCapture, bool inPrimaryLiquid, bool inReserveLiquid,
            bool primaryAlreadySelected, bool reserveAlreadySelected,
            bool nativeReady)
        {
            if (toolId != "research-pipette-visual" || !directlyHeld ||
                !deliberatePress || receiverCapture || !nativeReady ||
                inPrimaryLiquid == inReserveLiquid)
                return ResearchAuxiliaryDeliveryIntent.None;
            if (inPrimaryLiquid && !primaryAlreadySelected)
                return ResearchAuxiliaryDeliveryIntent.PipetteSelectPrimary;
            if (inReserveLiquid && !reserveAlreadySelected)
                return ResearchAuxiliaryDeliveryIntent.PipetteSelectReserve;
            return ResearchAuxiliaryDeliveryIntent.None;
        }
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
            return Resolve(physicalToolId, directlyHeld, deliberatePress,
                exclusiveReceiverCapture, immersedInSource, false, nativeReady);
        }

        public static ResearchAuxiliaryDeliveryIntent Resolve(
            string physicalToolId, bool directlyHeld, bool deliberatePress,
            bool exclusiveReceiverCapture, bool immersedInSource,
            bool capturedBuretteInlet, bool nativeReady)
        {
            if (!directlyHeld || !deliberatePress || !nativeReady ||
                (exclusiveReceiverCapture ? 1 : 0) +
                (immersedInSource ? 1 : 0) + (capturedBuretteInlet ? 1 : 0) != 1)
                return ResearchAuxiliaryDeliveryIntent.None;
            return physicalToolId switch
            {
                "research-stock-bottle-visual" when capturedBuretteInlet =>
                    ResearchAuxiliaryDeliveryIntent.BurettePrime,
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
