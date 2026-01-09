// Minimal TTS client module
(function () {
  window.TTS = window.TTS || {};

  const LS_KEYS = {
    enabled: 'tts.enabled',
    voice: 'tts.voice',
    volume: 'tts.volume',
    rate: 'tts.rate',
    pitch: 'tts.pitch',
    serverFile: 'tts.serverFile'
  };

  let voices = [];
  let audioEl = null;
  // Restore previous behavior: allow speech immediately (no manual per-tab gating)
  let audioUnlocked = true;

  function sanitizeText(text) {
    if (!text) return '';
    // strip HTML tags
    text = text.replace(/<[^>]*>/g, '');
    // collapse whitespace
    text = text.replace(/\s+/g, ' ').trim();
    if (text.length > 300) text = text.substring(0, 300) + '...';
    return text;
  }

  function loadVoices() {
    voices = speechSynthesis.getVoices() || [];
    const sel = document.getElementById('ttsVoiceSelect');
    if (!sel) return;
    const cur = localStorage.getItem(LS_KEYS.voice) || '';
    sel.innerHTML = '';
    voices.forEach(v => {
      const o = document.createElement('option');
      o.value = v.name;
      o.textContent = `${v.name} (${v.lang})`;
      if (v.name === cur) o.selected = true;
      sel.appendChild(o);
    });
  }

  function speakText(text, opts) {
    if (!('speechSynthesis' in window)) return;
    if (!text) return;
    text = sanitizeText(text);
    const utter = new SpeechSynthesisUtterance(text);
    const sel = document.getElementById('ttsVoiceSelect');
    const vol = parseFloat(localStorage.getItem(LS_KEYS.volume) || '1');
    utter.volume = isNaN(vol) ? 1 : vol;
    const rate = parseFloat(localStorage.getItem(LS_KEYS.rate) || '1');
    utter.rate = isNaN(rate) ? 1 : rate;
    const pitch = parseFloat(localStorage.getItem(LS_KEYS.pitch) || '1');
    utter.pitch = isNaN(pitch) ? 1 : pitch;
    if (sel && sel.value) {
      const voice = voices.find(v => v.name === sel.value);
      if (voice) utter.voice = voice;
    }
      try {
      console.log('TTS: speakText start', text.slice(0,120));
      utter.onstart = function () { console.log('TTS: utter onstart'); };
      utter.onend = function () { console.log('TTS: utter onend'); };
      utter.onerror = function (e) {
        console.error('TTS utterror', e);
        try {
          // attempt a fallback: speak without explicit voice (let browser pick)
          console.log('TTS: attempting fallback speak without voice');
          const fallback = new SpeechSynthesisUtterance(text);
          fallback.onstart = function () { console.log('TTS fallback onstart'); };
          fallback.onend = function () { console.log('TTS fallback onend'); };
          fallback.onerror = function (ee) { console.error('TTS fallback error', ee); };
          // set language to navigator locale as a hint
          try { fallback.lang = navigator.language || 'en-US'; } catch(e2) {}
          try { /* avoid cancelling here to prevent canceled errors */ } catch(e3){}
          speechSynthesis.speak(fallback);
        } catch (xx) {
          console.error('TTS fallback failed', xx);
        }
      };
      // ensure voices are loaded
      if (!voices || voices.length === 0) {
        loadVoices();
      }
      // Speak without preemptive cancel to avoid 'canceled' issues
      speechSynthesis.speak(utter);
      console.log('TTS: speakText queued');
    } catch (e) {
      console.error('speak error', e);
    }
  }

  async function fetchTtsFiles() {
    try {
      const res = await fetch('/tts/files');
      if (!res.ok) return;
      const files = await res.json();
      const sel = document.getElementById('ttsServerSelect');
      if (!sel) return;
      sel.innerHTML = '';
      files.forEach(f => {
        const o = document.createElement('option');
        o.value = f.url;
        o.textContent = f.name;
        sel.appendChild(o);
      });
      const saved = localStorage.getItem(LS_KEYS.serverFile);
      if (saved) sel.value = saved;
    } catch (e) {
      console.error('fetch tts files', e);
    }
  }

  function playServerTtsFile(url) {
    console.log('playServerTtsFile', url);
    if (!audioEl) {
      audioEl = document.getElementById('ttsAudio');
    }
    if (!audioEl) {
      audioEl = document.createElement('audio');
      audioEl.id = 'ttsAudio';
      document.body.appendChild(audioEl);
    }
    audioEl.src = url;
    audioEl.play().then(() => {
      console.log('audio play started');
    }).catch(e => {
      console.error('audio play failed', e);
      showToast('Playback blocked. Click "Enable Audio" to allow sounds in this tab.');
      tryResumeAudioContext();
    });
    audioEl.onended = function () {
      console.log('audio ended');
      // After playback, reload voices but avoid cancelling speech synthesis here
      setTimeout(() => { loadVoices(); }, 150);
    };
  }

  function resetTts() {
    try {
      try { speechSynthesis.cancel(); } catch(e){}
      if (audioEl) {
        audioEl.pause();
        audioEl.src = '';
      }
      voices = [];
      loadVoices();
      showToast('TTS reset');
      console.log('TTS reset performed');
    } catch (e) { console.error('resetTts error', e); }
  }

  function tryResumeAudioContext() {
    try {
      const Ctx = window.AudioContext || window.webkitAudioContext;
      if (!Ctx) return;
      let ctx = new Ctx();
      if (ctx && ctx.state === 'suspended') {
        ctx.resume().then(() => { audioUnlocked = true; console.log('AudioContext resumed'); }).catch(()=>{});
      } else {
        audioUnlocked = true;
      }
    } catch (e) { }
  }

  function showToast(msg, timeoutMs) {
    timeoutMs = timeoutMs || 4000;
    try {
      const containerId = 'ttsToastContainer';
      let container = document.getElementById(containerId);
      if (!container) {
        container = document.createElement('div');
        container.id = containerId;
        container.style.position = 'fixed';
        container.style.right = '12px';
        container.style.bottom = '12px';
        container.style.zIndex = 2000;
        document.body.appendChild(container);
      }
      const d = document.createElement('div');
      d.className = 'alert alert-warning';
      d.style.marginTop = '6px';
      d.textContent = msg;
      container.appendChild(d);
      setTimeout(() => { d.remove(); }, timeoutMs);
    } catch (e) { console.error('toast error', e); }
  }

  function handleIncomingChat(user, message) {
    try {
      const enabled = (localStorage.getItem(LS_KEYS.enabled) === null) ? true : (localStorage.getItem(LS_KEYS.enabled) === 'true');
      const checkbox = document.getElementById('ttsEnabled');
      if (checkbox && checkbox.checked !== enabled) checkbox.checked = enabled;
      if (!enabled) return;
      const text = `${user}: ${message}`;
      try {
        speakText(text);
      } catch (e) {
        console.error('speak error', e);
      }
    } catch (e) { console.error(e); }
  }

  function saveSettings() {
    const enabledEl = document.getElementById('ttsEnabled');
    if (enabledEl) localStorage.setItem(LS_KEYS.enabled, enabledEl.checked ? 'true' : 'false');
    const sel = document.getElementById('ttsVoiceSelect');
    if (sel) localStorage.setItem(LS_KEYS.voice, sel.value);
    const vol = document.getElementById('ttsVolume');
    if (vol) localStorage.setItem(LS_KEYS.volume, vol.value);
    const serverSel = document.getElementById('ttsServerSelect');
    if (serverSel) localStorage.setItem(LS_KEYS.serverFile, serverSel.value);
  }

  function restoreSettings() {
    try {
      const enabled = (localStorage.getItem(LS_KEYS.enabled) === null) ? true : (localStorage.getItem(LS_KEYS.enabled) === 'true');
      const enabledEl = document.getElementById('ttsEnabled');
      if (enabledEl) enabledEl.checked = enabled;
      const vol = localStorage.getItem(LS_KEYS.volume);
      const volEl = document.getElementById('ttsVolume');
      if (volEl && vol) volEl.value = vol;
    } catch (e) { }
  }

  function initializeTts() {
    // Ensure DOM is ready
    try {
      restoreSettings();
      const voiceSel = document.getElementById('ttsVoiceSelect');
      if (voiceSel) voiceSel.addEventListener('change', saveSettings);
      const enabledEl = document.getElementById('ttsEnabled');
      if (enabledEl) enabledEl.addEventListener('change', saveSettings);
      const volEl = document.getElementById('ttsVolume');
      if (volEl) volEl.addEventListener('input', saveSettings);
      // The play button is handled in the main client (`schafkopf.js`) to
      // optionally broadcast the selection via SignalR. Keep only fetchTtsFiles
      // here to populate the select element.
      const enableBtn = document.getElementById('ttsEnableAudioBtn');
      if (enableBtn) {
        enableBtn.addEventListener('click', function () {
          tryResumeAudioContext();
          audioUnlocked = true;
          showToast('Audio enabled for this tab');
        });
      }

      if ('speechSynthesis' in window) {
        loadVoices();
        window.speechSynthesis.onvoiceschanged = function () {
          loadVoices();
        };
      }
      fetchTtsFiles();
      // expose handler
      window.TTS.handleIncomingChat = handleIncomingChat;
      window.TTS.initializeTts = initializeTts;
      window.TTS.speakText = speakText;
      window.TTS.playServerTtsFile = playServerTtsFile;
      window.TTS.resetTts = resetTts;
      window.TTS.unlockAudio = function() {
        tryResumeAudioContext();
        audioUnlocked = true;
        // Prime speechSynthesis with a short utterance on user gesture to ensure it's allowed
        try {
          if ('speechSynthesis' in window) {
            const u = new SpeechSynthesisUtterance('Audio enabled');
            u.volume = 0.01;
            u.rate = 1;
            u.pitch = 1;
            u.onend = function() { console.log('TTS: unlock utterance ended'); };
            u.onerror = function(e) { console.warn('TTS unlock utterance error', e); };
            speechSynthesis.speak(u);
          }
        } catch (e) { console.error('unlockAudio error', e); }
      };
    } catch (e) {
      console.error('initializeTts error', e);
    }
  }

  // auto-init on script load if DOM ready
  if (document.readyState === 'complete' || document.readyState === 'interactive') {
    setTimeout(() => { try { initializeTts(); } catch (e) { } }, 300);
  } else {
    document.addEventListener('DOMContentLoaded', function () { setTimeout(() => { try { initializeTts(); } catch (e) { } }, 300); });
  }

})();
