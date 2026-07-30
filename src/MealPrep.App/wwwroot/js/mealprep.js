window.mealPrep = (() => {
    let wakeLock = null;

    async function requestWakeLock() {
        if (!("wakeLock" in navigator)) {
            return false;
        }

        try {
            wakeLock = await navigator.wakeLock.request("screen");
            wakeLock.addEventListener("release", () => {
                wakeLock = null;
            });
            return true;
        } catch {
            return false;
        }
    }

    async function releaseWakeLock() {
        if (wakeLock) {
            await wakeLock.release();
            wakeLock = null;
        }
        return false;
    }

    async function toggleWakeLock() {
        return wakeLock ? releaseWakeLock() : requestWakeLock();
    }

    document.addEventListener("visibilitychange", () => {
        if (document.visibilityState === "visible" && wakeLock === null) {
            return;
        }
    });

    return { toggleWakeLock, releaseWakeLock };
})();
