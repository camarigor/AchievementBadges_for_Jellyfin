(function () {
    var ENTRY_ID = "achievement-badges-nav-entry";
    var TARGET_URL = "/web/index.html#/configurationpage?name=achievementbadges";

    function createNavEntry() {
        if (document.getElementById(ENTRY_ID)) return;
        if (document.getElementById("achievementsSidebarEntry")) return;

        var sidebar =
            document.querySelector(".mainDrawer-scrollContainer .navMenuOptions") ||
            document.querySelector(".mainDrawer-scrollContainer .itemsContainer") ||
            document.querySelector(".mainDrawer-scrollContainer");

        if (!sidebar) return;

        var link = document.createElement("a");
        link.id = ENTRY_ID;
        link.href = TARGET_URL;
        link.className = "navMenuOption";
        link.style.display = "flex";
        link.style.alignItems = "center";
        link.style.gap = "1em";
        link.style.padding = "0.85em 1em";
        link.style.color = "inherit";
        link.style.textDecoration = "none";

        link.innerHTML =
            '<span class="material-icons navMenuOptionIcon" style="font-size:1.2em;">emoji_events</span>' +
            '<span class="navMenuOptionText">Achievements</span>';

        sidebar.appendChild(link);
    }

    function init() {
        createNavEntry();
        var observer = new MutationObserver(function () { createNavEntry(); });
        observer.observe(document.body, { childList: true, subtree: true });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
