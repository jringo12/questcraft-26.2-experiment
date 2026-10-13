package pojlib.util;

import java.util.HashMap;

import pojlib.util.json.MinecraftInstances;

/**
 * H6 fallback: if the graphics backend a player picked fails during game
 * initialisation, the NEXT launch of that instance automatically uses the
 * other backend instead of hard-failing on the same backend again.
 *
 * Mechanism: a small marker file records the backend that just failed and
 * when. {@link #getEffectiveRenderer} consults it at launch time. The marker
 * is time-boxed (30 minutes) and one-shot in effect — an explicit renderer
 * change in the UI clears it, so the player's own choice always wins when
 * they intervene, and a stale marker can never override a choice forever.
 */
public class RendererFallback {
    private static final long MARKER_TTL_MS = 30 * 60 * 1000L;

    public static class FallbackEntry {
        public String failedBackend;
        public long timestampMs;
    }

    public static class FallbackState {
        public HashMap<String, FallbackEntry> instances = new HashMap<>();
    }

    private static String markerPath() {
        return Constants.USER_HOME + "/renderer-fallback.json";
    }

    private static FallbackState loadState() {
        try {
            FallbackState state = GsonUtils.jsonFileToObject(markerPath(), FallbackState.class);
            if (state == null) {
                return new FallbackState();
            }
            if (state.instances == null) {
                state.instances = new HashMap<>();
            }
            return state;
        } catch (Exception e) {
            // Corrupt marker file: ignore it rather than breaking the launch.
            return new FallbackState();
        }
    }

    private static void saveState(FallbackState state) {
        try {
            GsonUtils.objectToJsonFile(markerPath(), state);
        } catch (Exception ignored) {
        }
    }

    public static String fallbackFor(String renderer) {
        return "opengl".equals(renderer) ? "vulkan" : "opengl";
    }

    /**
     * Backend to actually launch with: the instance's chosen renderer, unless
     * that backend just failed to initialise for this instance, in which case
     * the other backend.
     */
    public static String getEffectiveRenderer(MinecraftInstances.Instance instance) {
        String chosen = instance.getRenderer();
        FallbackState state = loadState();
        FallbackEntry entry = state.instances.get(instance.instanceName);
        if (entry != null
                && chosen.equals(entry.failedBackend)
                && System.currentTimeMillis() - entry.timestampMs < MARKER_TTL_MS) {
            String fallback = fallbackFor(chosen);
            Logger.getInstance().appendToLog(
                    "Renderer fallback: " + chosen + " failed to initialise last launch, trying " + fallback + " for instance " + instance.instanceName);
            return fallback;
        }
        return chosen;
    }

    /** Record that {@code backend} failed during game initialisation. */
    public static void recordInitFailure(MinecraftInstances.Instance instance, String backend) {
        FallbackState state = loadState();
        FallbackEntry entry = new FallbackEntry();
        entry.failedBackend = backend;
        entry.timestampMs = System.currentTimeMillis();
        state.instances.put(instance.instanceName, entry);
        saveState(state);
        Logger.getInstance().appendToLog(
                "Renderer fallback: recorded init failure of " + backend + " for instance " + instance.instanceName);
    }

    /** Clear any pending fallback for this instance (explicit UI choice wins). */
    public static void clearMarker(MinecraftInstances.Instance instance) {
        FallbackState state = loadState();
        if (state.instances.remove(instance.instanceName) != null) {
            saveState(state);
        }
    }
}
