async function updateVersion() {
    try {
        const response = await fetch('http://localhost:9001/version');
        const data = await response.json();
        if (data.version) {
            document.getElementById('version-text').innerText = 'v' + data.version;
        }
    } catch (e) {
        // App is not running, maybe fetch from GitHub as fallback?
        try {
            const githubResponse = await fetch('https://raw.githubusercontent.com/groxbe/ZERTER/main/version.txt');
            const version = await githubResponse.text();
            if (version) {
                document.getElementById('version-text').innerText = 'v' + version.trim();
            }
        } catch (err) {
            console.log("Could not fetch version.");
        }
    }
}

updateVersion();
