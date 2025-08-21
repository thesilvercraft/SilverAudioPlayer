// Check if server is in read-only mode and disable relevant controls
async function checkReadOnlyAndDisable() {
    try {
        const resp = await fetch('/checkifreadonly');
        if (!resp.ok) return;
        const isReadOnly = await resp.text();
        if (isReadOnly === 'true' || isReadOnly === 'True') {
            // List of button IDs to disable
            const ids = [
                'btn-play', 'btn-pause', 'btn-playpause', 'btn-next', 'btn-previous', 'btn-stop', 'progressbar'
            ];
            for (const id of ids) {
                const el = document.getElementById(id);
                if (el) el.disabled = true;
            }
            // Show warning if present
            const warn = document.getElementById('readonly-warning');
            if (warn) warn.style.display = '';
        }
    } catch {}
}

let lastTrackId = null;
let lastTitle = null;
let lastArtist = null;
let lastAlbum = null;
let lastDuration = 0;
let seeking = false;

async function callEndpoint(endpoint) {
    const responseBox = document.getElementById('response');
    responseBox.textContent = 'Loading...';
    try {
        const res = await fetch('/' + endpoint);
        let text = await res.text();
        // Try to pretty-print JSON
        try { text = JSON.stringify(JSON.parse(text), null, 2); } catch { }
        responseBox.textContent = text;
    } catch (e) {
        responseBox.textContent = 'Error: ' + e;
    }
}

function refreshAlbumArt(force) {
    const img = document.getElementById('albumart');
    img.style.display = 'block';
    // Add a cache buster
    img.src = '/albumart?t=' + Date.now();
}

function formatTime(sec) {
    if (!isFinite(sec) || sec < 0) return '0:00';
    sec = Math.floor(sec);
    let m = Math.floor(sec / 60);
    let s = sec % 60;
    return m + ':' + (s < 10 ? '0' : '') + s;
}

let lastLyrics = [];
let lastLyricIdx = -1;
// Render all synced lyrics with timestamps, highlight current
function renderFullLyricsBlock(currentIdx = -1) {
    if(!showingFullLyrics) return;
    var fullBlock = document.getElementById('full-lyrics-block');
    if (!fullBlock) return;
    if (lastLyrics.length > 0) {
        let html = '';
        for (let i = 0; i < lastLyrics.length; ++i) {
            const l = lastLyrics[i];
            let min = Math.floor(l.timeStampInMilliSeconds / 60000);
            let sec = Math.floor((l.timeStampInMilliSeconds % 60000) / 1000);
            let ts = `[${min}:${sec.toString().padStart(2, '0')}]`;
            let highlight = (i === currentIdx) ? 'background:#e3eaff;font-weight:bold;color:#1976d2;border-radius:0.3em;' : '';
            html += `<div id="lyric-line-${i}" style="margin-bottom:0.2em;${highlight}"><span style="color:#888;font-size:0.95em;">${ts}</span> ${l.content.replace(/</g,'&lt;').replace(/>/g,'&gt;')}</div>`;
        }
        fullBlock.innerHTML = html;
        fullBlock.style.display = '';
    } else {
        fullBlock.textContent = '';
        fullBlock.style.display = '';
    }
}
async function pollNowPlaying() {
    try {
        const [trackRes, posRes, durRes] = await Promise.all([
            fetch('/track'),
            fetch('/position'),
            fetch('/duration')
        ]);
        let trackData = trackRes.ok ? await trackRes.json() : {};
        let pos = posRes.ok ? parseFloat(await posRes.text()) : 0;
        let dur = durRes.ok ? parseFloat(await durRes.text()) : 0;

        // Extract from metadata if present
        let meta = trackData.metadata || {};
        let title = meta.title || trackData.title || trackData.name || '';
        let artist = meta.artist || trackData.artist || '';
        let album = meta.album || trackData.album || '';
        // duration in ms in metadata, fallback to /duration endpoint (seconds)
        let metaDur = meta.duration ? (meta.duration / 1000) : null;
        if (!dur && metaDur) dur = metaDur;
        let trackId = (trackData.guid || trackData.id || title + artist + album);

        // Update only if changed
        if (trackId !== lastTrackId) {
            lastTrackId = trackId;
            lastTitle = title;
            lastArtist = artist;
            lastAlbum = album;
            refreshAlbumArt(true);
            // Lyrics
            lastLyrics = Array.isArray(meta.syncedLyrics) ? meta.syncedLyrics : [];
            lastLyricIdx = -1;
        }
        document.getElementById('nowplaying-title').textContent = title || '(No track)';
        document.getElementById('nowplaying-artist').textContent = artist ? 'Artist: ' + artist : '';
        document.getElementById('nowplaying-album').textContent = album ? 'Album: ' + album : '';

        // Progress bar
        let progress = document.getElementById('progressbar');
        let posSpan = document.getElementById('nowplaying-pos');
        let durSpan = document.getElementById('nowplaying-dur');
        if (!seeking) {
            progress.max = dur || 1;
            progress.value = pos || 0;
        }
        posSpan.textContent = formatTime(pos);
        durSpan.textContent = formatTime(dur);
        lastDuration = dur;

        // Synced lyrics block
        let lyricsBlock = document.getElementById('synced-lyrics-block');
        if (lastLyrics.length > 0) {
            // Find the last lyric whose timestamp is <= pos*1000
            let idx = -1;
            for (let i = 0; i < lastLyrics.length; ++i) {
                if (lastLyrics[i].timeStampInMilliSeconds <= pos * 1000) idx = i;
                else break;
            }
            lastLyricIdx = idx;
            // Show previous, current, next lines (centered)
            let html = '';
            for (let offset = -2; offset <= 2; ++offset) {
                let showIdx = idx + offset;
                if (showIdx < 0 || showIdx >= lastLyrics.length) continue;
                let c = lastLyrics[showIdx].content;
                if (offset === 0) {
                    html += `<div style="font-weight:bold;color:#1976d2;font-size:1.25em;background:#e3eaff;border-radius:0.3em;margin:0.2em 0;">${c}</div>`;
                } else {
                    html += `<div style="color:#888;margin:0.1em 0;">${c}</div>`;
                }
            }
            lyricsBlock.innerHTML = html;
            // Also update full lyrics block to highlight current line
            renderFullLyricsBlock(idx);
        } else {
            lyricsBlock.innerHTML = '';
            renderFullLyricsBlock(-1);
        }
    } catch {
        document.getElementById('nowplaying-title').textContent = '(No track)';
        document.getElementById('nowplaying-artist').textContent = '';
        document.getElementById('nowplaying-album').textContent = '';
        let progress = document.getElementById('progressbar');
        progress.value = 0;
        progress.max = 1;
        document.getElementById('nowplaying-pos').textContent = '0:00';
        document.getElementById('nowplaying-dur').textContent = '0:00';
        document.getElementById('synced-lyrics-block').innerHTML = '';
    }
}


// --- Lyrics toggle and autoscroll logic ---
let showingFullLyrics = false;
let autoscrollLyrics = true;

function showFullLyricsBlock(show) {
    const syncedBlock = document.getElementById('synced-lyrics-block');
    const fullBlock = document.getElementById('full-lyrics-block');
    const expandBtn = document.getElementById('expand-lyrics-btn');
    const autoBtn = document.getElementById('autoscroll-lyrics-btn');
    if (show) {
        syncedBlock.style.display = 'none';
        fullBlock.style.display = '';
        expandBtn.textContent = 'Show Synced Lyrics';
        autoBtn.style.display = '';
        renderFullLyricsBlock(lastLyricIdx);
    } else {
        syncedBlock.style.display = '';
        fullBlock.style.display = 'none';
        expandBtn.textContent = 'Show All Lyrics';
        autoBtn.style.display = 'none';
    }
}

function scrollToCurrentLyric(idx) {
    const el = document.getElementById('lyric-line-' + idx);
    if (el) {
    var fullBlock = document.getElementById('full-lyrics-block');
        const blockRect = fullBlock.getBoundingClientRect();
        const elRect = el.getBoundingClientRect();
        if (elRect.top < blockRect.top || elRect.bottom > blockRect.bottom) {
            el.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
    }
}

document.addEventListener('DOMContentLoaded', function () {
    checkReadOnlyAndDisable();
    const progress = document.getElementById('progressbar');
    if (progress) {
        progress.addEventListener('input', function () {
            seeking = true;
            document.getElementById('nowplaying-pos').textContent = formatTime(progress.value);
        });
        progress.addEventListener('change', async function () {
            seeking = false;
            let val = progress.value;
            try {
                await fetch('/position?position=' + encodeURIComponent(val), {
                    method: 'POST',
                    headers: { 'Accept': '*/*' },
                    body: ''
                });
            } catch (e) {
                document.getElementById('response').textContent = 'Seek failed: ' + e;
            }
        });
    }

    const expandBtn = document.getElementById('expand-lyrics-btn');
    const autoBtn = document.getElementById('autoscroll-lyrics-btn');
    if (expandBtn && autoBtn) {
        expandBtn.addEventListener('click', function () {
            showingFullLyrics = !showingFullLyrics;
            showFullLyricsBlock(showingFullLyrics);
        });
        autoBtn.addEventListener('click', function () {
            autoscrollLyrics = !autoscrollLyrics;
            autoBtn.textContent = 'Autoscroll: ' + (autoscrollLyrics ? 'On' : 'Off');
        });
        // Set initial state
        showFullLyricsBlock(false);
        autoBtn.textContent = 'Autoscroll: On';
    }
});

// --- Autoscroll for full lyrics block ---
let lastAutoscrollIdx = -2;
setInterval(function () {
    if (showingFullLyrics && autoscrollLyrics && lastLyricIdx !== lastAutoscrollIdx && lastLyricIdx >= 0) {
        scrollToCurrentLyric(lastLyricIdx);
        lastAutoscrollIdx = lastLyricIdx;
    }
}, 500);

setInterval(pollNowPlaying, 1000);
pollNowPlaying();
