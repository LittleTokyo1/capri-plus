(function () {
  "use strict";
  var KEY = "capri.token";
  var TYPE_PER_SECOND = 40;
  var PING_MS = 30000;
  var REST = 1.11;
  var token = read();
  var clips = {};
  var images = {};
  var body = { name: "idle", start: 0, once: false };
  var nextBlink = 0;
  var talkingUntil = 0;
  var mouthCell = 0;
  var mouthAt = 0;
  var typing = null;
  var POSE_MS = 400;
  var POSE_FRESH_MS = 3000;
  var STANDING = ["idle", "blink", "blink_glitch"];
  var pose = { left: false, talking: false, at: -1e9 };
  var jobs = [];
  var VOICE_KEY = "capri.voice";
  var MOST_SECONDS = 30;
  var LEAST_SECONDS = 0.3;
  var RATE = 16000;
  var voiceOn = readVoice();
  var sound = null;
  var holding = false;
  var recording = null;
  var voiceEndAt = 0;
  var EVENTS_RETRY_MS = 3000;
  var lastEvent = -1;
  var polling = false;

  var POLL_LIMIT_MS = 40000;
  var RESYNC_AFTER_MS = 30000;
  var pollAbort = null;
  var pollRun = 0;
  var hiddenAt = 0;

  function events() {
    if (polling || !token || document.hidden || $("room").hidden) return;
    polling = true;
    var run = ++pollRun;
    var abort = window.AbortController ? new AbortController() : null;
    pollAbort = abort;
    var timer = abort ? setTimeout(function () { abort.abort(); }, POLL_LIMIT_MS) : 0;
    var options = { headers: { Authorization: "Bearer " + token }, cache: "no-store" };
    if (abort) options.signal = abort.signal;
    fetch("api/events?after=" + lastEvent, options).then(function (answer) {
      if (answer.status === 401) { unpair(); return null; }
      return answer.status === 200 ? answer.json() : null;
    }).then(function (got) {
      clearTimeout(timer);
      if (run !== pollRun) return;
      polling = false;
      if (!got) { if (token) setTimeout(events, EVENTS_RETRY_MS); return; }
      (got.events || []).forEach(function (one) { if (one.id > lastEvent) ownLine(one); });
      lastEvent = got.last < lastEvent ? got.last : Math.max(lastEvent, got.last);
      events();
    }).catch(function () {
      clearTimeout(timer);
      if (run !== pollRun) return;
      polling = false;
      setTimeout(events, EVENTS_RETRY_MS);
    });
  }

  function restartEvents() {
    var old = pollAbort;
    pollAbort = null;
    pollRun++;
    polling = false;
    if (old) old.abort();
    events();
  }

  function history() {
    if (!token) return Promise.resolve();
    return fetch("api/history", { headers: { Authorization: "Bearer " + token }, cache: "no-store" }).then(function (answer) {
      if (answer.status === 401) { unpair(); return null; }
      return answer.status === 200 ? answer.json() : null;
    }).then(function (kept) {
      if (!kept) return;
      $("log").textContent = "";
      (kept.lines || []).forEach(function (one) { line(one.text, one.mine ? "mine" : "hers"); });
      lastEvent = -1;
    }).catch(function () { });
  }

  function resync() { history().then(restartEvents); }

  function woke() {
    if (document.hidden) { hiddenAt = performance.now(); return; }
    ping();
    if (hiddenAt && performance.now() - hiddenAt > RESYNC_AFTER_MS) resync(); else restartEvents();
    hiddenAt = 0;
  }

  function ownLine(one) {
    lastEvent = one.id;
    $("youLine").textContent = "";
    $("bubble").hidden = false;
    herSays(one.text, false);
    if (!document.hidden) post("api/ack", { id: one.id }, true).catch(function () { });
  }

  function readVoice() { try { return localStorage.getItem(VOICE_KEY) !== "0"; } catch (e) { return true; } }
  function keepVoice(on) { voiceOn = on; try { localStorage.setItem(VOICE_KEY, on ? "1" : "0"); } catch (e) { } showVoice(); }
  function showVoice() { $("voiceOn").className = "on" + (voiceOn ? " picked" : ""); $("voiceOff").className = "off" + (voiceOn ? "" : " picked"); }

  function context() {
    if (!sound) { var Made = window.AudioContext || window.webkitAudioContext; if (!Made) return null; sound = new Made(); }
    if (sound.state === "suspended") sound.resume();
    return sound;
  }

  function $(id) { return document.getElementById(id); }
  function read() { try { return localStorage.getItem(KEY); } catch (e) { return null; } }
  function keep(value) { try { if (value) localStorage.setItem(KEY, value); else localStorage.removeItem(KEY); } catch (e) { } token = value; }

  function state(text, kind) { var shown = $("state"); shown.textContent = text; shown.className = "state " + (kind || ""); }

  function show(paired) {
    $("pair").hidden = paired;
    $("room").hidden = !paired;
    $("pcKey").hidden = !paired;
    if (!paired) closePc();
    if (paired) { ping(); loadClips(); resync(); } else { state("not paired", "off"); }
  }

  function post(path, fields, signed) {
    var headers = { "Content-Type": "application/x-www-form-urlencoded" };
    if (signed) headers.Authorization = "Bearer " + token;
    return fetch(path, { method: "POST", headers: headers, body: new URLSearchParams(fields).toString(), cache: "no-store" });
  }

  function pair() {
    var code = $("code").value.trim().toUpperCase();
    var note = $("pairNote");
    if (code.length !== 6) { note.textContent = "The code has 6 letters and numbers."; return; }
    $("pairKey").disabled = true;
    note.textContent = "Pairing...";
    post("pair", { code: code, name: "iPhone" }, false).then(function (answer) {
      if (answer.status === 200) return answer.json().then(function (made) { keep(made.token); $("code").value = ""; note.textContent = ""; show(true); });
      note.textContent = answer.status === 403 ? "Wrong or old code, or pairing is locked for a few minutes. Make a new code on the PC." : "Capri answered " + answer.status + ".";
    }).catch(function () {
      note.textContent = "Can't reach the PC. Is Capri running, the phone app on in Capri+, and Tailscale on here?";
    }).then(function () { $("pairKey").disabled = false; });
  }

  function unpair() { keep(null); show(false); $("pairNote").textContent = "This phone isn't paired any more. Make a new code on the PC."; }

  function ping() {
    if (!token) return;
    fetch("api/ping", { headers: { Authorization: "Bearer " + token }, cache: "no-store" }).then(function (answer) {
      if (answer.status === 401) { unpair(); return; }
      state(answer.ok ? "online" : "error " + answer.status, answer.ok ? "on" : "off");
    }).catch(function () { state("offline", "off"); });
  }

  function line(text, kind) {
    var item = document.createElement("li");
    item.className = kind;
    item.textContent = text;
    var log = $("log");
    log.appendChild(item);
    log.scrollTop = log.scrollHeight;
  }

  function say(event) {
    event.preventDefault();
    var box = $("say");
    var text = box.value.trim();
    if (!text) return;
    box.value = "";
    grow();
    youSaid(text);
    $("sayKey").disabled = true;
    state("thinking", "on");
    context();
    answered(post("api/say", { text: text, voice: voiceOn ? "1" : "0" }, true)).then(function () { $("sayKey").disabled = false; });
  }

  function grow() {
    var box = $("say");
    box.style.height = "auto";
    box.style.height = box.scrollHeight + "px";
  }

  function typed(event) {
    if (event.key !== "Enter" || event.shiftKey || document.body.classList.contains("chatfull")) return;
    event.preventDefault();
    say(event);
  }

  function fullChat() {
    var on = document.body.classList.toggle("chatfull");
    $("chatFull").setAttribute("aria-pressed", on ? "true" : "false");
    grow();
    var log = $("log");
    log.scrollTop = log.scrollHeight;
  }

  function youSaid(text) {
    line(text, "mine");
    $("youLine").textContent = "> you: " + text;
    $("herLine").textContent = "";
    $("caret").className = "caret";
    $("bubble").hidden = false;
  }

  function answered(asked) {
    return asked.then(function (answer) {
      if (answer.status === 401) { unpair(); return; }
      if (answer.status === 429) { herSays("I'm still on your last line, one moment.", false); return; }
      if (answer.status === 503) { herSays("My ears aren't working on the PC right now.", false); return; }
      if (!answer.ok) { herSays("Something went wrong on the PC (" + answer.status + ").", false); return; }
      return answer.json().then(function (made) {
        if (made.heard === "") { state("online", "on"); line("I didn't catch that. Hold the key while you talk.", "sys"); return; }
        if (made.heard) youSaid(made.heard);
        var voiced = voiceOn && made.turn > 0;
        herSays(made.said || "...", voiced);
        if (voiced) playVoice(made.turn);
      });
    }).catch(function () {
      herSays("I can't reach the PC right now.", false);
      state("offline", "off");
    });
  }

  function playVoice(turn) {
    var player = context();
    if (!player) return;
    fetch("api/voice?turn=" + turn, { headers: { Authorization: "Bearer " + token }, cache: "no-store" }).then(function (answer) {
      if (answer.status !== 200) return null;
      return answer.arrayBuffer();
    }).then(function (bytes) {
      if (!bytes) return;
      player.decodeAudioData(bytes, function (decoded) {
        var source = player.createBufferSource();
        source.buffer = decoded;
        source.connect(player.destination);
        source.start(0);
        talkingUntil = performance.now() + decoded.duration * 1000;
        voiceEndAt = talkingUntil;
      }, function () { });
    }).catch(function () { });
  }

  function startTalk(event) {
    event.preventDefault();
    if (holding) return;
    holding = true;
    if (event.pointerId !== undefined && event.target.setPointerCapture) event.target.setPointerCapture(event.pointerId);
    var player = context();
    var key = $("talkKey");
    key.className = "key talk held";
    key.textContent = "LISTENING";
    if (!player || !navigator.mediaDevices) { stopTalk(); line("This browser can't use the mic here.", "sys"); return; }
    navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true } }).then(function (stream) {
      if (!holding) { stream.getTracks().forEach(function (track) { track.stop(); }); return; }
      var track = stream.getAudioTracks()[0];
      var settings = track && track.getSettings ? track.getSettings() : {};
      var mic = settings.sampleRate || 0;
      var capture = listener(mic);
      if (capture.state === "suspended") capture.resume();
      var source = capture.createMediaStreamSource(stream);
      var node = capture.createScriptProcessor(4096, 1, 1);
      var taken = { stream: stream, capture: capture, source: source, node: node, parts: [], length: 0, rate: capture.sampleRate, mic: mic, began: performance.now(), rec: null, chunks: [] };
      if (window.MediaRecorder) {
        try {
          taken.rec = new MediaRecorder(stream);
          taken.rec.ondataavailable = function (event) { if (event.data && event.data.size > 0) taken.chunks.push(event.data); };
          taken.rec.start();
        } catch (e) { taken.rec = null; }
      }
      node.onaudioprocess = function (event) {
        var data = event.inputBuffer.getChannelData(0);
        taken.parts.push(new Float32Array(data));
        taken.length += data.length;
        if (taken.length / taken.rate > MOST_SECONDS) stopTalk();
      };
      source.connect(node);
      node.connect(capture.destination);
      recording = taken;
    }).catch(function () {
      holding = false;
      resetTalk();
      line("I can't use your mic. Allow the microphone for this page (Settings > Safari > Microphone).", "sys");
    });
  }

  function listener(mic) {
    var Made = window.AudioContext || window.webkitAudioContext;
    if (mic) { try { return new Made({ sampleRate: mic }); } catch (e) { } }
    return new Made();
  }

  function resetTalk() { var key = $("talkKey"); key.className = "key talk"; key.textContent = "HOLD TO TALK"; }

  function stopTalk() {
    holding = false;
    resetTalk();
    var taken = recording;
    recording = null;
    if (!taken) return;
    var held = Math.round(performance.now() - taken.began);
    taken.node.disconnect();
    taken.source.disconnect();
    var closed = function () {
      taken.stream.getTracks().forEach(function (track) { track.stop(); });
      if (taken.capture.close) taken.capture.close();
    };
    if (held < LEAST_SECONDS * 1000) { if (taken.rec && taken.rec.state !== "inactive") taken.rec.stop(); closed(); line("Hold the key while you talk.", "sys"); return; }
    state("listening", "on");
    $("talkKey").disabled = true;
    var tapped = function () { return { samples: downsample(taken.parts, taken.length, taken.rate), rate: taken.rate, how: "tap" }; };
    if (!taken.rec || taken.rec.state === "inactive") { closed(); sent(taken, held, tapped()); return; }
    taken.rec.onstop = function () {
      closed();
      recorded(taken).then(function (made) { sent(taken, held, made); }, function () { sent(taken, held, tapped()); });
    };
    taken.rec.stop();
  }

  function recorded(taken) {
    return new Promise(function (resolve, reject) {
      if (!taken.chunks.length) { reject(); return; }
      var blob = new Blob(taken.chunks, { type: taken.rec.mimeType || "audio/mp4" });
      var reader = new FileReader();
      reader.onerror = reject;
      reader.onload = function () {
        var player = context();
        if (!player) { reject(); return; }
        var done = function (decoded) {
          var data = decoded.getChannelData(0);
          if (!data.length) { reject(); return; }
          resolve({ samples: downsample([data], data.length, decoded.sampleRate), rate: decoded.sampleRate, how: "rec" });
        };
        var asked = player.decodeAudioData(reader.result, done, reject);
        if (asked && asked.catch) asked.catch(reject);
      };
      reader.readAsArrayBuffer(blob);
    });
  }

  function sent(taken, held, made) {
    var standalone = window.navigator.standalone === true || (window.matchMedia && window.matchMedia("(display-mode: standalone)").matches);
    var samples = made.samples.length > RATE * MOST_SECONDS ? made.samples.slice(0, RATE * MOST_SECONDS) : made.samples;
    var played = voiceEndAt ?Math.max(1, Math.round(taken.began - voiceEndAt)) : 0;
    var query = "voice=" + (voiceOn ? "1" : "0") + "&rate=" + Math.round(made.rate) + "&mic=" + Math.round(taken.mic) + "&held=" + held + "&how=" + made.how + "&app=" + (standalone ? "1" : "0") + "&played=" + played;
    answered(fetch("api/hear?" + query, { method: "POST", headers: { Authorization: "Bearer " + token, "Content-Type": "application/octet-stream" }, body: samples.buffer, cache: "no-store" }))
      .then(function () { $("talkKey").disabled = false; });
  }

  function downsample(parts, length, rate) {
    var merged = new Float32Array(length);
    var at = 0;
    parts.forEach(function (part) { merged.set(part, at); at += part.length; });
    var step = rate / RATE;
    var count = Math.floor(length / step);
    var out = new Int16Array(count);
    for (var i = 0; i < count; i++) {
      var from = Math.floor(i * step), to = Math.min(length, Math.floor((i + 1) * step));
      var sum = 0, n = 0;
      for (var j = from; j < to; j++) { sum += merged[j]; n++; }
      var value = Math.max(-1, Math.min(1, n ? sum / n : 0));
      out[i] = value < 0 ? value * 0x8000 : value * 0x7fff;
    }
    return out;
  }

  function herSays(text, voiced) {
    state("online", "on");
    line(text, "hers");
    if (typing) clearInterval(typing);
    var shown = 0;
    var target = $("herLine");
    talkingUntil = voiced ? 0 : performance.now() + text.length * 1000 / TYPE_PER_SECOND + 400;
    typing = setInterval(function () {
      shown = Math.min(text.length, shown + 2);
      target.textContent = text.slice(0, shown);
      if (shown >= text.length) { clearInterval(typing); typing = null; $("caret").className = "caret done"; }
    }, 2000 / TYPE_PER_SECOND);
  }

  function item(text, at, from) {
    var one = document.createElement("li");
    if (at) { var time = document.createElement("span"); time.className = "at"; time.textContent = at; one.appendChild(time); }
    if (from) { var source = document.createElement("span"); source.className = "from"; source.textContent = from; one.appendChild(source); }
    one.appendChild(document.createTextNode(text));
    return one;
  }

  function fill(id, items, none) {
    var list = $(id);
    list.textContent = "";
    if (!items.length) { var empty = item(none); empty.className = "none"; list.appendChild(empty); return; }
    items.forEach(function (one) { list.appendChild(one); });
  }

  function pcStatus() {
    fetch("api/pc", { headers: { Authorization: "Bearer " + token }, cache: "no-store" }).then(function (answer) {
      if (answer.status === 401) { unpair(); return null; }
      return answer.status === 200 ? answer.json() : null;
    }).then(function (seen) {
      if (!seen) { $("pcNote").textContent = "I can't reach the PC right now."; return; }
      $("pcNote").textContent = seen.paused ? "Capri is paused on the PC." : seen.game ? "A game is running on the PC." : "";
      fill("jobs", (seen.jobs || []).map(function (text) { return item(text); }), "Nothing running.");
      fill("digest", (seen.digest || []).map(function (one) { return item(one.text, one.at, one.source); }), "Nothing waiting for you.");
    }).catch(function () { $("pcNote").textContent = "I can't reach the PC right now."; });
  }

  function look() {
    $("lookKey").disabled = true;
    $("pcNote").textContent = "Looking...";
    fetch("api/shot", { headers: { Authorization: "Bearer " + token }, cache: "no-store" }).then(function (answer) {
      if (answer.status === 401) { unpair(); return null; }
      if (answer.status === 409) return answer.text().then(function (why) { $("pcNote").textContent = why; return null; });
      if (answer.status !== 200) { $("pcNote").textContent = "I couldn't take a screenshot just now."; return null; }
      return answer.blob();
    }).then(function (picture) {
      if (!picture) return;
      var reader = new FileReader();
      reader.onload = function () { var shot = $("shot"); shot.src = reader.result; shot.hidden = false; $("pcNote").textContent = ""; };
      reader.readAsDataURL(picture);
    }).catch(function () { $("pcNote").textContent = "I can't reach the PC right now."; }).then(function () { $("lookKey").disabled = false; });
  }

  var settingsKept = [];

  function press(key) {
    $("pcNote").textContent = "...";
    post("api/key", { key: key }, true).then(function (answer) {
      if (answer.status === 401) { unpair(); return null; }
      if (answer.status === 429) return { said: "I'm still on your last line, one moment." };
      return answer.ok ? answer.json() : null;
    }).then(function (made) {
      $("pcNote").textContent = made ? made.said : "I can't reach the PC right now.";
      pcStatus();
    }).catch(function () { $("pcNote").textContent = "I can't reach the PC right now."; });
  }

  function loadSettings() {
    fetch("api/settings", { headers: { Authorization: "Bearer " + token }, cache: "no-store" }).then(function (answer) {
      if (answer.status === 401) { unpair(); return null; }
      return answer.status === 200 ? answer.json() : null;
    }).then(function (got) { settingsKept = got && got.settings ? got.settings : []; showSettings(); }).catch(function () { });
  }

  function showSettings() {
    var asked = $("setFind").value.trim().toLowerCase();
    var list = $("settings");
    list.textContent = "";
    settingsKept.filter(function (one) { return !asked || (one.words + " " + one.key).toLowerCase().indexOf(asked) >= 0; }).slice(0, 40).forEach(function (one) {
      var row = document.createElement("li");
      row.className = "setting";
      var name = document.createElement("span");
      name.className = "from";
      name.textContent = one.words;
      row.appendChild(name);
      var pick = document.createElement("select");
      one.choices.forEach(function (choice) { var option = document.createElement("option"); option.value = choice; option.textContent = choice; if (choice === one.now) option.selected = true; pick.appendChild(option); });
      pick.addEventListener("change", function () { change(one.key, pick.value); });
      row.appendChild(pick);
      list.appendChild(row);
    });
  }

  function change(key, value) {
    post("api/setting", { key: key, value: value }, true).then(function (answer) {
      if (answer.status === 401) { unpair(); return null; }
      return answer.ok ? answer.json() : null;
    }).then(function (made) { $("pcNote").textContent = made ? made.said : "I couldn't change that just now."; loadSettings(); }).catch(function () { $("pcNote").textContent = "I can't reach the PC right now."; });
  }
  function openPc() { $("pcSheet").hidden = false; $("shot").hidden = true; $("shot").removeAttribute("src"); pcStatus(); loadSettings(); }
  function closePc() { $("pcSheet").hidden = true; $("shot").hidden = true; $("shot").removeAttribute("src"); closeShortcuts(); }
  function openShortcuts() { $("scSheet").hidden = false; $("scHome").textContent = location.origin; $("scNote").textContent = ""; }
  function closeShortcuts() { $("scSheet").hidden = true; $("scKey").textContent = ""; $("scKeyRow").hidden = true; }

  function makeKey() {
    $("scMake").disabled = true;
    post("api/shortcutkey", {}, true).then(function (answer) {
      if (answer.status === 401) { unpair(); return null; }
      return answer.ok ? answer.json() : null;
    }).then(function (made) {
      if (!made || !made.token) { $("scNote").textContent = "I can't reach the PC right now."; return; }
      $("scKey").textContent = made.token;
      $("scKeyRow").hidden = false;
      $("scNote").textContent = "Copy it now. I show it only this once.";
    }).catch(function () { $("scNote").textContent = "I can't reach the PC right now."; }).then(function () { $("scMake").disabled = false; });
  }

  function copyKey() {
    var text = $("scKey").textContent;
    if (!text || !navigator.clipboard) return;
    navigator.clipboard.writeText(text).then(function () { $("scNote").textContent = "Copied. Paste it after Bearer in each shortcut."; }).catch(function () { });
  }
  function loadClips() {
    if (clips.idle) return;
    fetch("clips").then(function (answer) { return answer.json(); }).then(function (list) {
      list.forEach(function (clip) { clips[clip.name] = clip; });
      ["idle", "blink", "mouth", "grin"].forEach(art);
      requestAnimationFrame(frame);
    }).catch(function () { });
  }

  function art(name, left) {
    if (!clips[name]) return null;
    var key = left && clips[name].flipped ? name + "_flipped" : name;
    if (!images[key]) { images[key] = new Image(); images[key].src = "sprite/" + key + ".png"; }
    return images[key].complete && images[key].naturalWidth > 0 ? images[key] : null;
  }

  function play(name, now) { if (clips[name]) body = { name: name, start: now, once: name !== "idle" }; }

  function follow() {
    if (!token || document.hidden || $("room").hidden || !clips.idle) return;
    fetch("api/state", { headers: { Authorization: "Bearer " + token }, cache: "no-store" }).then(function (answer) {
      return answer.status === 200 ? answer.json() : null;
    }).then(function (seen) {
      if (!seen || !clips[seen.clip]) return;
      var now = performance.now();
      pose = { left: !!seen.left, talking: seen.mouth >= 0, at: now };
      jobs = Array.isArray(seen.jobs) ? seen.jobs : [];
      if (body.once && body.name === "grin") return;
      art(seen.clip, pose.left);
      var clip = clips[seen.clip];
      var start = now - seen.cell / clip.fps * 1000;
      if (body.name !== seen.clip) { body = { name: seen.clip, start: start, once: false }; return; }
      var local = Math.floor((now - body.start) / 1000 * clip.fps) % clip.count;
      var drift = Math.abs(local - seen.cell);
      if (Math.min(drift, clip.count - drift) > 2) body.start = start;
    }).catch(function () { });
  }

  function frame(now) {
    requestAnimationFrame(frame);
    if (document.hidden) return;
    var canvas = $("her");
    var ratio = window.devicePixelRatio || 1;
    var wide = Math.round(canvas.clientWidth * ratio), tall = Math.round(canvas.clientHeight * ratio);
    if (canvas.width !== wide || canvas.height !== tall) { canvas.width = wide; canvas.height = tall; }
    if (!nextBlink) nextBlink = now + 3000;
    var clip = clips[body.name];
    var cell = Math.floor((now - body.start) / 1000 * clip.fps);
    if (body.once && cell >= clip.count) { play("idle", now); clip = clips.idle; cell = 0; }
    var led = now - pose.at < POSE_FRESH_MS;
    if (!led && body.name === "idle" && now > nextBlink) { play("blink", now); nextBlink = now + 3000 + Math.random() * 4000; clip = clips.blink; cell = 0; }
    var mirrored = led && pose.left;
    var image = art(body.name, mirrored) || art(body.name) || art("idle");
    if (!image) return;
    if (!art(body.name, mirrored) && !art(body.name)) clip = clips.idle;
    cell = cell % clip.count;
    var draw = canvas.getContext("2d");
    draw.clearRect(0, 0, wide, tall);
    var w = image.naturalWidth / clip.count, h = image.naturalHeight;
    var scale = tall * 0.97 / h * (clip.height / REST);
    var dw = w * scale, dh = h * scale, x = (wide - dw) / 2 + clip.x * dh, y = tall - dh + clip.y * dh;
    draw.save();
    if (mirrored) { draw.translate(wide, 0); draw.scale(-1, 1); }
    draw.drawImage(image, cell * w, 0, w, h, x, y, dw, dh);
    var mouth = art("mouth");
    var talking = now < talkingUntil || (led && pose.talking);
    if (mouth && talking && STANDING.indexOf(body.name) >= 0) {
      if (now > mouthAt) { mouthCell = 1 + Math.floor(Math.random() * (clips.mouth.count - 1)); mouthAt = now + 1000 / clips.mouth.fps; }
      var mw = mouth.naturalWidth / clips.mouth.count;
      draw.drawImage(mouth, mouthCell * mw, 0, mw, mouth.naturalHeight, x, y, dw, dh);
    }
    draw.restore();
    if (led) badges(draw, mirrored ? wide - x - dw : x, y, dw, dh, wide, now);
  }

  function badges(draw, x, y, dw, dh, wide, now) {
    if (!jobs.length) return;
    var size = Math.max(18, dh * 0.075), gap = size * 0.15;
    var bx = Math.min(wide - size - 4, x + dw * 0.68), by = y + dh * 0.1;
    var thinking = jobs.some(function (job) { return !job.done && job.kind !== "project"; });
    if (thinking) {
      draw.fillStyle = "#e84246";
      for (var d = 0; d < 3; d++) {
        draw.globalAlpha = 0.35 + 0.65 * (0.5 + 0.5 * Math.sin(now / 1000 * 6 - d * 0.9));
        draw.beginPath();
        draw.arc(bx + size * (0.25 + d * 0.25), by + size * 0.25, size * 0.08, 0, Math.PI * 2);
        draw.fill();
      }
      draw.globalAlpha = 1;
      by += size * 0.55;
    }
    jobs.forEach(function (job, i) {
      var top = by + i * (size + gap), cx = bx + size / 2, cy = top + size / 2, u = size / 28;
      draw.fillStyle = "#090b0f";
      draw.strokeStyle = "#e84246";
      draw.lineWidth = Math.max(1, u * 1.2);
      draw.fillRect(bx, top, size, size);
      draw.strokeRect(bx, top, size, size);
      draw.strokeStyle = job.done ? (job.ok ? "#5ef0f6" : "#e84246") : "#ececf1";
      draw.lineWidth = Math.max(1.5, u * (job.done ? 2.4 : 1.6));
      draw.beginPath();
      if (job.done && job.ok) { draw.moveTo(cx - 6 * u, cy); draw.lineTo(cx - 2 * u, cy + 4 * u); draw.lineTo(cx + 6 * u, cy - 5 * u); }
      else if (job.done) { draw.moveTo(cx - 5 * u, cy - 5 * u); draw.lineTo(cx + 5 * u, cy + 5 * u); draw.moveTo(cx + 5 * u, cy - 5 * u); draw.lineTo(cx - 5 * u, cy + 5 * u); }
      else if (job.kind === "mail") { draw.rect(cx - 7 * u, cy - 5 * u, 14 * u, 10 * u); draw.moveTo(cx - 7 * u, cy - 5 * u); draw.lineTo(cx, cy + u); draw.lineTo(cx + 7 * u, cy - 5 * u); }
      else if (job.kind === "chat") { draw.rect(cx - 7 * u, cy - 6 * u, 14 * u, 9 * u); draw.moveTo(cx - 3 * u, cy + 3 * u); draw.lineTo(cx - 5 * u, cy + 7 * u); draw.lineTo(cx + 1 * u, cy + 3 * u); }
      else if (job.kind === "study") { draw.rect(cx - 7 * u, cy - 5 * u, 6.5 * u, 10 * u); draw.rect(cx + 0.5 * u, cy - 5 * u, 6.5 * u, 10 * u); }
      else { draw.moveTo(cx - 3 * u, cy - 5 * u); draw.lineTo(cx - 7 * u, cy); draw.lineTo(cx - 3 * u, cy + 5 * u); draw.moveTo(cx + 3 * u, cy - 5 * u); draw.lineTo(cx + 7 * u, cy); draw.lineTo(cx + 3 * u, cy + 5 * u); }
      draw.stroke();
    });
  }

  $("pairKey").addEventListener("click", pair);
  $("pcKey").addEventListener("click", function () { if ($("pcSheet").hidden) openPc(); else closePc(); });
  $("pcBack").addEventListener("click", closePc);
  $("scKeyOpen").addEventListener("click", openShortcuts);
  $("scBack").addEventListener("click", closeShortcuts);
  $("scMake").addEventListener("click", makeKey);
  $("scCopy").addEventListener("click", copyKey);
  $("lookKey").addEventListener("click", look);
  $("stopKey").addEventListener("click", function () { press("stop"); });
  $("pauseKey").addEventListener("click", function () { press("pause"); });
  $("wakeKey").addEventListener("click", function () { press("wake"); });
  $("setFind").addEventListener("input", showSettings);
  $("code").addEventListener("keydown", function (event) { if (event.key === "Enter") pair(); });
  $("typeBox").addEventListener("submit", say);
  $("say").addEventListener("input", grow);
  $("say").addEventListener("keydown", typed);
  $("chatFull").addEventListener("click", fullChat);
  $("her").addEventListener("click", function () { play("grin", performance.now()); });
  $("talkKey").addEventListener("pointerdown", startTalk);
  $("talkKey").addEventListener("pointerup", stopTalk);
  $("talkKey").addEventListener("pointercancel", stopTalk);
  $("talkKey").addEventListener("contextmenu", function (event) { event.preventDefault(); });
  $("voiceOn").addEventListener("click", function () { context(); keepVoice(true); });
  $("voiceOff").addEventListener("click", function () { keepVoice(false); });
  showVoice();
  document.addEventListener("visibilitychange", woke);
  setInterval(function () { if (!document.hidden) ping(); }, PING_MS);
  setInterval(follow, POSE_MS);
  if ("serviceWorker" in navigator) {
    var hadWorker = !!navigator.serviceWorker.controller;
    navigator.serviceWorker.addEventListener("controllerchange", function () { if (hadWorker) location.reload(); hadWorker = true; });
    navigator.serviceWorker.register("sw.js").catch(function () { });
  }
  show(!!token);
})();
