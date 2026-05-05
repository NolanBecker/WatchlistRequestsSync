(function () {
    "use strict";

    const pluginId = "7b2cef2e-d5ea-43e2-be8e-ab2070b2d18e";
    const pageId = "WatchlistRequestsSyncConfigPage";

    function byId(id) {
        return document.getElementById(id);
    }

    function renderUsers(users) {
        const container = byId("userSettingsContainer");
        container.innerHTML = "";

        users.forEach(function (user, index) {
            const wrapper = document.createElement("div");
            wrapper.className = "userCard";
            wrapper.innerHTML = [
                "<h3>" + user.jellyfinUserName + "</h3>",
                "<label>Enable Sync <input type='checkbox' data-field='isEnabled' " + (user.settings.isEnabled ? "checked" : "") + " /></label>",
                "<label>Seerr User ID <input type='text' data-field='seerrUserId' value='" + (user.settings.seerrUserId || "") + "' /></label>",
                "<label>Optional Media Tag <input type='text' data-field='mediaTag' value='" + (user.settings.mediaTag || "") + "' /></label>",
                "<label>Include Movies <input type='checkbox' data-field='includeMovies' " + (user.settings.includeMovies ? "checked" : "") + " /></label>",
                "<label>Include Series <input type='checkbox' data-field='includeSeries' " + (user.settings.includeSeries ? "checked" : "") + " /></label>",
                "<label>Include Pending <input type='checkbox' data-field='includePendingRequests' " + (user.settings.includePendingRequests ? "checked" : "") + " /></label>",
                "<label>Include Approved <input type='checkbox' data-field='includeApprovedRequests' " + (user.settings.includeApprovedRequests ? "checked" : "") + " /></label>",
                "<label>Include Available <input type='checkbox' data-field='includeAvailableRequests' " + (user.settings.includeAvailableRequests ? "checked" : "") + " /></label>"
            ].join("");
            wrapper.dataset.index = index.toString();
            container.appendChild(wrapper);
        });
    }

    async function fetchUsers() {
        const users = await ApiClient.getJSON(ApiClient.getUrl("Plugins/WatchlistRequestsSync/Users"));
        renderUsers(users);
        return users;
    }

    async function showStatus() {
        const status = await ApiClient.getJSON(ApiClient.getUrl("Plugins/WatchlistRequestsSync/Status"));
        byId("statusOutput").textContent = JSON.stringify(status, null, 2);
    }

    function captureUsers(existingUsers) {
        const cards = Array.from(document.querySelectorAll("#userSettingsContainer .userCard"));
        return cards.map(function (card) {
            const index = parseInt(card.dataset.index, 10);
            const original = existingUsers[index];
            const updated = JSON.parse(JSON.stringify(original.settings));

            card.querySelectorAll("[data-field]").forEach(function (field) {
                if (field.type === "checkbox") {
                    updated[field.dataset.field] = field.checked;
                } else {
                    updated[field.dataset.field] = field.value;
                }
            });

            updated.jellyfinUserId = original.jellyfinUserId;
            updated.jellyfinUserName = original.jellyfinUserName;
            return updated;
        });
    }

    Dashboard.registerPluginPage(pageId, {
        pluginId: pluginId,
        name: "Watchlist Requests Sync",
        view: function (view) {
            let users = [];

            view.addEventListener("viewshow", async function () {
                const config = await ApiClient.getPluginConfiguration(pluginId);
                byId("enablePlugin").checked = config.isEnabled;
                byId("baseUrl").value = config.seerrBaseUrl || "";
                byId("apiKey").value = config.apiKey || "";
                byId("syncIntervalMinutes").value = config.syncIntervalMinutes || 360;
                byId("dryRun").checked = config.dryRun;
                users = await fetchUsers();
                await showStatus();
            });

            byId("watchlistRequestsSyncForm").addEventListener("submit", async function (event) {
                event.preventDefault();
                const config = await ApiClient.getPluginConfiguration(pluginId);
                config.isEnabled = byId("enablePlugin").checked;
                config.seerrBaseUrl = byId("baseUrl").value;
                config.apiKey = byId("apiKey").value;
                config.syncIntervalMinutes = parseInt(byId("syncIntervalMinutes").value || "360", 10);
                config.dryRun = byId("dryRun").checked;
                config.users = captureUsers(users);
                await ApiClient.updatePluginConfiguration(pluginId, config);
                Dashboard.processPluginConfigurationUpdateResult();
            });

            byId("testConnectionButton").addEventListener("click", async function () {
                const result = await ApiClient.ajax({
                    type: "POST",
                    url: ApiClient.getUrl("Plugins/WatchlistRequestsSync/TestConnection"),
                    dataType: "json"
                });
                byId("statusOutput").textContent = JSON.stringify(result, null, 2);
            });

            byId("previewSyncButton").addEventListener("click", async function () {
                const result = await ApiClient.ajax({
                    type: "POST",
                    url: ApiClient.getUrl("Plugins/WatchlistRequestsSync/PreviewSync"),
                    dataType: "json"
                });
                byId("statusOutput").textContent = JSON.stringify(result, null, 2);
            });

            byId("runSyncButton").addEventListener("click", async function () {
                const result = await ApiClient.ajax({
                    type: "POST",
                    url: ApiClient.getUrl("Plugins/WatchlistRequestsSync/RunSync"),
                    dataType: "json"
                });
                byId("statusOutput").textContent = JSON.stringify(result, null, 2);
            });
        }
    });
})();
