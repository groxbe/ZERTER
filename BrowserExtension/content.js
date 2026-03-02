let lastUrl = "";
let checkInterval = null;

async function checkStatus(btn, url) {
    try {
        const response = await fetch(`http://localhost:9001/check?url=${encodeURIComponent(url)}`);
        const data = await response.json();

        const textElement = btn.querySelector(".zerter-text");
        if (!textElement) return;

        switch (data.status) {
            case "waiting":
                textElement.innerText = "Bekliyor...";
                btn.style.background = "#FFB74D";
                break;
            case "downloading":
                textElement.innerText = "İndiriliyor...";
                btn.style.background = "#4FC3F7";
                break;
            case "downloaded":
                textElement.innerText = "Mevcut";
                btn.style.background = "#81C784";
                break;
            case "none":
            default:
                if (!btn.classList.contains("success")) {
                    textElement.innerText = "ZERTER ile İndir";
                    btn.style.background = "#BB86FC";
                }
                break;
        }
    } catch (e) {
        const textElement = btn.querySelector(".zerter-text");
        if (textElement && !btn.classList.contains("success")) {
            textElement.innerText = "ZERTER (Bağlantı Yok)";
            btn.style.background = "#555";
        }
    }
}

function injectButton() {
    const isMusic = window.location.hostname === "music.youtube.com";
    const selector = isMusic
        ? "ytmusic-player-bar .middle-controls-buttons"
        : "#top-level-buttons-computed";

    const container = document.querySelector(selector);
    if (!container) return;

    let btn = container.querySelector(".zerter-download-btn");

    if (!btn) {
        btn = document.createElement("button");
        btn.className = "zerter-download-btn " + (isMusic ? "music" : "main");
        btn.innerHTML = `
            <span class="zerter-text">ZERTER ile İndir</span>
        `;

        btn.onclick = (e) => {
            e.stopPropagation();
            const url = window.location.href;
            navigator.clipboard.writeText(url).then(() => {
                const textElement = btn.querySelector(".zerter-text");
                textElement.innerText = "Eklendi!";
                btn.classList.add("success");
                setTimeout(() => {
                    btn.classList.remove("success");
                    checkStatus(btn, window.location.href);
                }, 2000);
            });
        };

        if (isMusic) {
            container.appendChild(btn);
        } else {
            container.insertBefore(btn, container.firstChild);
        }
    }

    if (window.location.href !== lastUrl) {
        lastUrl = window.location.href;
        checkStatus(btn, lastUrl);
    }
}

function startPolling() {
    if (checkInterval) clearInterval(checkInterval);
    checkInterval = setInterval(() => {
        const btn = document.querySelector(".zerter-download-btn");
        if (btn) checkStatus(btn, window.location.href);
        injectButton();
    }, 3000);
}

// Initial
injectButton();
startPolling();

window.addEventListener("yt-navigate-finish", injectButton);
window.addEventListener("url-change", injectButton); 
