package org.libsdl.app;

/**
 * Stub for SDL3's {@code org.libsdl.app.HIDDeviceManager}.
 *
 * SDL's {@code JNI_OnLoad} (libSDL3.so) calls {@code FindClass} for this
 * class and {@code RegisterNatives} with its 8-method table. If the class
 * (or any listed method) is missing, {@code RegisterNatives} fails and
 * leaves a pending exception that aborts the launcher during bridge
 * setup (6.0.5-RC3 silent-death bug). This stub exists ONLY so
 * {@code JNI_OnLoad} completes cleanly: QuestCraft never creates a
 * {@code HIDDeviceManager}, so these methods never run. The declarations
 * must match SDL's {@code HIDDeviceManager_tab} exactly (name + JNI
 * signature). NOTE: unlike the other four SDL classes, HID's methods
 * are INSTANCE (non-static) natives — the {@code static} keyword must
 * NOT be added, or {@code RegisterNatives} will fail to match.
 */
public final class HIDDeviceManager {
    private HIDDeviceManager() {}

    private native void HIDDeviceRegisterCallback();

    private native void HIDDeviceReleaseCallback();

    private native void HIDDeviceConnected(int deviceID, String identifier, int vendorId,
            int productId, String serial_number, int release_number,
            String manufacturer_string, String product_string,
            int interface_number, int interface_class, int interface_subclass,
            int interface_protocol, boolean bBluetooth, int reportID);

    private native void HIDDeviceOpenPending(int deviceID);

    private native void HIDDeviceOpenResult(int deviceID, boolean opened);

    private native void HIDDeviceDisconnected(int deviceID);

    private native void HIDDeviceInputReport(int deviceID, byte[] report);

    private native void HIDDeviceReportResponse(int deviceID, byte[] report);
}
