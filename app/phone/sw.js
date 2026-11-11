"use strict";
var VERSION = "__VERSION__";
var CACHE = "capri-" + VERSION;
var SHELL = ["./", "index.html", "app.js", "app.css", "manifest.webmanifest", "icon.png", "clips", "mono.ttf", "font.ttf"];

function kept(url) {
  var path = new URL(url).pathname;
  if (path.indexOf("/api/") === 0 || path === "/pair" || path === "/sw.js") return false;
  return true;
}

self.addEventListener("install", function (event) {
  event.waitUntil(caches.open(CACHE).then(function (cache) {
    return Promise.all(SHELL.map(function (name) { return cache.add(name).catch(function () { }); }));
  }).then(function () { return self.skipWaiting(); }));
});

self.addEventListener("activate", function (event) {
  event.waitUntil(caches.keys().then(function (names) {
    return Promise.all(names.filter(function (name) { return name.indexOf("capri-") === 0 && name !== CACHE; }).map(function (name) { return caches.delete(name); }));
  }).then(function () { return self.clients.claim(); }));
});

self.addEventListener("fetch", function (event) {
  var request = event.request;
  if (request.method !== "GET" || new URL(request.url).origin !== self.location.origin || !kept(request.url)) return;
  var key = request.mode === "navigate" ? "index.html" : request;
  event.respondWith(caches.open(CACHE).then(function (cache) {
    return cache.match(key).then(function (hit) {
      if (hit) return hit;
      return fetch(request).then(function (answer) {
        if (answer.ok) cache.put(key, answer.clone());
        return answer;
      });
    });
  }));
});
