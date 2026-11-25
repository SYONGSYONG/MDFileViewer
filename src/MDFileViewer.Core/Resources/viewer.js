// 뷰어 템플릿(viewer.html)이 읽는 앱 스크립트 (.claude/02-specs.md §17.16, 01 F-25).
// 본문의 ```mermaid 코드 블록을 그림(SVG)으로 바꾼다. 앱이 본문·테마를 바꾼 뒤 window.mdfv.renderDiagrams()를 부른다.
// 인쇄·내보내기(01 F-26, 02 §17.17) 전에는 앱이 prepareOutput()을 부르고 isOutputReady()가 참이 되기를 기다린다 — 결과물은 늘 밝은 테마라,
// 어두운 테마로 그려 둔 그림 옆에 밝은 그림을 숨겨서 넣어 둔다(인쇄용 스타일이 그쪽을 보여 준다). exportHtml()은 그 밝은 그림으로 바꾼 본문을 준다.
//
// 문서 안 스크립트는 CSP(script-src는 앱 폴더뿐)가 막는다. 이 파일과 mermaid.min.js만 실행된다.
// 본문의 원시 HTML이 document.getElementById 같은 속성을 덮을 수 있으므로(DOM clobbering, ViewerScriptBuilder 참고)
// 이 파일은 document.x를 읽지 않고, 덮을 수 없는 프로토타입의 메서드를 call로 부른다. 필요한 것은 본문이 들어오기 전인 지금 잡아 둔다.
(function () {
  'use strict';

  var CONTENT_ID = 'content';
  var LIBRARY_URL = 'https://appassets.example/mermaid.min.js';
  var BLOCK_SELECTOR = 'pre > code.language-mermaid';
  var DIAGRAM_CLASS = 'diagram';
  var FAILED_ATTRIBUTE = 'data-diagram-failed';
  var THEME_ATTRIBUTE = 'data-theme';
  var DARK = 'dark';
  var LIGHT = 'default';
  var LIGHT_COPY_CLASS = 'diagram-light';
  var HAS_LIGHT_ATTRIBUTE = 'data-has-light';

  // 그린 결과를 기억하는 개수. 넘으면 전부 비운다 — 편집 미리보기는 입력마다 본문을 다시 넣으므로 바뀌지 않은 그림을 다시 그리지 않게 한다
  var CACHE_LIMIT = 200;

  // 편집기에서 찾는 말을 미리보기에 칠하는 강조의 이름(viewer.css의 ::highlight와 같아야 한다)과 칠하는 곳의 한도.
  // 한두 글자를 찾으면 수만 곳이 될 수 있다 — 넘는 곳은 칠하지 않는다(개수와 바꾸기는 편집기가 기준이다)
  var FIND_HIGHLIGHT = 'mdfv-find';
  var FIND_HIGHLIGHT_LIMIT = 5000;

  var doc = document;
  var getElementById = Document.prototype.getElementById;
  var createElement = Document.prototype.createElement;
  var rootElement = Object.getOwnPropertyDescriptor(Document.prototype, 'documentElement').get.call(doc);
  var headElement = Object.getOwnPropertyDescriptor(Document.prototype, 'head').get.call(doc);
  var querySelectorAll = Element.prototype.querySelectorAll;
  var getAttribute = Element.prototype.getAttribute;
  var removeAttribute = Element.prototype.removeAttribute;
  var scrollIntoView = Element.prototype.scrollIntoView;
  var closest = Element.prototype.closest;
  var getRect = Element.prototype.getBoundingClientRect;

  // 블록에 단 원본 줄 번호(AppConstants.SourceLineAttribute와 같아야 한다)
  var LINE_ATTRIBUTE = 'data-line';
  var setAttribute = Element.prototype.setAttribute;
  var appendChild = Node.prototype.appendChild;
  var textContentOf = Object.getOwnPropertyDescriptor(Node.prototype, 'textContent').get;
  var parentOf = Object.getOwnPropertyDescriptor(Node.prototype, 'parentElement').get;
  var setInnerHtml = Object.getOwnPropertyDescriptor(Element.prototype, 'innerHTML').set;
  var setClassName = Object.getOwnPropertyDescriptor(Element.prototype, 'className').set;
  var replaceWith = Element.prototype.replaceWith;
  var removeElement = Element.prototype.remove;
  var cloneNode = Node.prototype.cloneNode;
  var getInnerHtml = Object.getOwnPropertyDescriptor(Element.prototype, 'innerHTML').get;
  var createTreeWalker = Document.prototype.createTreeWalker;

  var library = null;        // 불러온 mermaid
  var libraryLoading = null; // 불러오는 중인 Promise
  var cache = new Map();     // 테마 + 원문 → SVG
  var sources = new WeakMap(); // 이 파일이 만든 그림 요소 → 원문. 본문이 흉내 낸 요소는 여기에 없다
  var generation = 0;        // 본문·테마가 다시 바뀌면 앞선 그리기를 그만둔다
  var sequence = 0;
  var queue = Promise.resolve(); // 그리기는 한 번에 하나씩 한다 — mermaid 설정(테마)이 전역이라 겹치면 다른 테마로 그려진다
  var outputReady = true;

  function currentTheme() {
    return getAttribute.call(rootElement, THEME_ATTRIBUTE) === DARK ? DARK : 'default';
  }

  // 그림이 든 문서를 처음 볼 때만 불러온다(3.6MB) — 그림이 없는 문서와 시작 시간에 부담을 주지 않는다
  function loadLibrary() {
    if (library) {
      return Promise.resolve(library);
    }

    if (!libraryLoading) {
      libraryLoading = new Promise(function (resolve, reject) {
        var script = createElement.call(doc, 'script');
        script.onload = function () {
          // 라이브러리가 window에 대입해 만든 자체 속성이라, 본문에 이름이 같은 요소(<img name="mermaid">)가 있어도 이쪽이 잡힌다
          var loaded = window.mermaid;
          if (loaded && typeof loaded.render === 'function') {
            library = loaded;
            resolve(loaded);
          } else {
            // 읽기 실패(onerror)와 같이 다음 호출에서 다시 시도하게 둔다
            libraryLoading = null;
            reject(new Error('mermaid를 불러왔지만 쓸 수 없습니다.'));
          }
        };
        script.onerror = function () {
          libraryLoading = null;
          reject(new Error('mermaid를 불러오지 못했습니다.'));
        };
        script.src = LIBRARY_URL;
        appendChild.call(headElement, script);
      });
    }

    return libraryLoading;
  }

  function show(target, source, svg) {
    var holder = createElement.call(doc, 'div');
    setClassName.call(holder, DIAGRAM_CLASS);
    setInnerHtml.call(holder, svg);
    sources.set(holder, source);
    replaceWith.call(target, holder);
  }

  // 그리지 못하면 라이브러리가 임시 요소를 body에 남긴다 — 문서 아래에 오류 그림이 쌓이지 않게 지운다
  function removeLeftovers(id) {
    var names = [id, 'd' + id, 'i' + id];
    for (var index = 0; index < names.length; index++) {
      var leftover = getElementById.call(doc, names[index]);
      if (leftover) {
        removeElement.call(leftover);
      }
    }
  }

  function enqueue(work) {
    queue = queue.then(work, work);
    return queue;
  }

  // strict: 그림 원문의 스크립트·이벤트 속성을 지우고 클릭 동작을 만들지 않는다. 스크립트를 막는 경계는 CSP이고 이것은 그 안쪽의 한 겹이다
  function configure(mermaid, theme) {
    mermaid.initialize({ startOnLoad: false, securityLevel: 'strict', theme: theme });
  }

  // 그리지 못하면(문법이 틀린 블록) null
  async function renderSvg(mermaid, source) {
    var id = 'mdfv-diagram-' + (++sequence);
    try {
      return (await mermaid.render(id, source)).svg;
    } catch (error) {
      removeLeftovers(id);
      return null;
    }
  }

  function remember(theme, source, svg) {
    if (cache.size >= CACHE_LIMIT) {
      cache.clear();
    }

    cache.set(theme + '\n' + source, svg);
  }

  // 그릴 대상: 아직 코드 블록인 것(<pre>를 통째로 바꾼다)과, 테마가 바뀌어 다시 그릴 이미 그린 그림
  function collectTargets(content) {
    var targets = [];
    var blocks = querySelectorAll.call(content, BLOCK_SELECTOR);
    for (var index = 0; index < blocks.length; index++) {
      var pre = parentOf.call(blocks[index]);
      if (pre && !getAttribute.call(pre, FAILED_ATTRIBUTE)) {
        targets.push({ element: pre, source: textContentOf.call(blocks[index]) });
      }
    }

    var drawn = querySelectorAll.call(content, 'div.' + DIAGRAM_CLASS);
    for (var drawnIndex = 0; drawnIndex < drawn.length; drawnIndex++) {
      var source = sources.get(drawn[drawnIndex]);
      if (typeof source === 'string') {
        targets.push({ element: drawn[drawnIndex], source: source });
      }
    }

    return targets;
  }

  async function renderPending(pending, theme, mine) {
    var mermaid = await loadLibrary();
    if (mine !== generation) {
      return;
    }

    configure(mermaid, theme);
    for (var index = 0; index < pending.length; index++) {
      var item = pending[index];

      // 문법이 틀린 블록은 null — 아래에서 원문 그대로 둔다
      var svg = await renderSvg(mermaid, item.source);

      // 그리는 사이 본문·테마가 바뀌었으면 결과를 버린다. 기억하지도 않는다 — 뒤 호출이 설정(테마)을 바꾼 뒤에 끝난 그림일 수 있다
      if (mine !== generation) {
        return;
      }

      if (typeof svg !== 'string') {
        // 다시 그리려 들지 않게 코드 블록에 표시해 둔다(본문이 바뀌면 새 요소라 다시 시도한다).
        // 테마를 바꿔 다시 그리던 그림이면 앞 테마의 그림을 그대로 둔다
        if (!sources.has(item.element)) {
          setAttribute.call(item.element, FAILED_ATTRIBUTE, '1');
        }

        continue;
      }

      remember(theme, item.source, svg);
      show(item.element, item.source, svg);
    }
  }

  // 어두운 테마로 그려 둔 그림마다 밝은 그림을 숨겨서 붙인다. 밝은 테마면 화면의 그림이 곧 결과물이라 할 일이 없다
  async function addLightCopies() {
    var content = getElementById.call(doc, CONTENT_ID);
    if (!content || currentTheme() !== DARK) {
      return;
    }

    var mermaid = null;
    var drawn = querySelectorAll.call(content, 'div.' + DIAGRAM_CLASS);
    for (var index = 0; index < drawn.length; index++) {
      var holder = drawn[index];
      var source = sources.get(holder);
      if (typeof source !== 'string' || getAttribute.call(holder, HAS_LIGHT_ATTRIBUTE)) {
        continue;
      }

      var svg = cache.get(LIGHT + '\n' + source);
      if (typeof svg !== 'string') {
        if (!mermaid) {
          mermaid = await loadLibrary();
          configure(mermaid, LIGHT);
        }

        svg = await renderSvg(mermaid, source);
        if (typeof svg !== 'string') {
          continue;
        }

        remember(LIGHT, source, svg);
      }

      var copy = createElement.call(doc, 'div');
      setClassName.call(copy, LIGHT_COPY_CLASS);
      setInnerHtml.call(copy, svg);
      appendChild.call(holder, copy);
      setAttribute.call(holder, HAS_LIGHT_ATTRIBUTE, '1');
    }
  }

  // 앞서 시작한 그리기가 끝난 뒤에 준비한다 — 아직 코드 블록인 그림이 결과물에 원문으로 나가지 않게
  function prepareOutput() {
    outputReady = false;
    enqueue(function () {
      return addLightCopies().catch(function () { });
    }).then(function () {
      outputReady = true;
    });
  }

  function isOutputReady() {
    return outputReady;
  }

  // 화면의 본문은 건드리지 않고 복사본에서 어두운 그림을 밝은 그림으로 바꿔 돌려준다
  function exportHtml() {
    var content = getElementById.call(doc, CONTENT_ID);
    if (!content) {
      return '';
    }

    var clone = cloneNode.call(content, true);
    var copies = querySelectorAll.call(clone, 'div.' + DIAGRAM_CLASS + '[' + HAS_LIGHT_ATTRIBUTE + '] > div.' + LIGHT_COPY_CLASS);
    for (var index = 0; index < copies.length; index++) {
      var holder = parentOf.call(copies[index]);
      var replacement = createElement.call(doc, 'div');
      setClassName.call(replacement, DIAGRAM_CLASS);
      setInnerHtml.call(replacement, getInnerHtml.call(copies[index]));
      replaceWith.call(holder, replacement);
    }

    // 줄 번호는 화면에서 줄을 찾는 데만 쓴다 — 내보낸 파일에는 남기지 않는다 (01 F-35)
    var marked = querySelectorAll.call(clone, '[' + LINE_ATTRIBUTE + ']');
    for (var markedIndex = 0; markedIndex < marked.length; markedIndex++) {
      removeAttribute.call(marked[markedIndex], LINE_ATTRIBUTE);
    }

    return getInnerHtml.call(clone);
  }

  // 원본의 line번 줄(1부터)이 든 블록을 화면 가운데쯤에 보여 준다 (01 F-35, 02 §17.25). 줄 번호가 line 이하인 블록 중 가장 큰 것이
  // 그 줄이 든 블록이다 — 문서 순서대로 커진다고 가정하지 않는다(각주는 정의가 어디 있든 문서 끝에 그려진다).
  // 그런 블록이 없으면 비율로 스크롤한다.
  // 본문의 원시 HTML이 data-line을 달고 있으면 엉뚱한 곳으로 갈 수 있다 — 스크롤 위치만 틀릴 뿐이다
  function scrollToLine(line, fallbackRatio) {
    var content = getElementById.call(doc, CONTENT_ID);
    var target = null;
    var targetLine = 0;
    if (content) {
      var blocks = querySelectorAll.call(content, '[' + LINE_ATTRIBUTE + ']');
      for (var index = 0; index < blocks.length; index++) {
        // 숫자가 아닌 값(본문의 원시 HTML이 단 것)은 NaN이라 아래 비교에서 걸러진다
        var blockLine = parseInt(getAttribute.call(blocks[index], LINE_ATTRIBUTE), 10);
        if (blockLine <= line && blockLine >= targetLine) {
          target = blocks[index];
          targetLine = blockLine;
        }
      }
    }

    if (target) {
      scrollIntoView.call(target, { block: 'center' });
      return;
    }

    window.scrollTo(0, fallbackRatio * Math.max(0, rootElement.scrollHeight - window.innerHeight));
  }

  // 각주는 정의가 원본 어디에 있든 문서 끝에 그려진다 — 편집기를 따라갈 때 세면 그 줄에서 문서 끝으로 튄다
  var FOOTNOTES_SELECTOR = '.footnotes';

  // 편집기의 맨 위 줄(line, 1부터, 줄 안의 위치는 소수)이 미리보기의 맨 위에 오게 스크롤한다 (01 F-36, 02 §17.26).
  // 그 줄을 사이에 둔 두 블록의 위치를 줄 번호로 나눠 잡는다 — 여러 줄짜리 문단·코드 블록 안에서도 부드럽게 따라간다.
  // 편집기가 맨 위·맨 아래면(ratio 0·1) 미리보기도 끝까지 간다. 줄 번호를 단 블록이 없으면 비율로 스크롤한다
  function syncToLine(line, totalLines, ratio) {
    var scrollable = Math.max(0, rootElement.scrollHeight - window.innerHeight);
    if (ratio <= 0 || ratio >= 1) {
      window.scrollTo(0, ratio >= 1 ? scrollable : 0);
      return;
    }

    var content = getElementById.call(doc, CONTENT_ID);
    var before = null;
    var beforeLine = 0;
    var after = null;
    var afterLine = Infinity;
    var blocks = content ? querySelectorAll.call(content, '[' + LINE_ATTRIBUTE + ']') : [];
    for (var index = 0; index < blocks.length; index++) {
      if (closest.call(blocks[index], FOOTNOTES_SELECTOR)) {
        continue;
      }

      // 숫자가 아닌 값(본문의 원시 HTML이 단 것)은 NaN이라 두 비교 모두에서 걸러진다
      var blockLine = parseInt(getAttribute.call(blocks[index], LINE_ATTRIBUTE), 10);
      if (blockLine <= line && blockLine >= beforeLine) {
        before = blocks[index];
        beforeLine = blockLine;
      } else if (blockLine > line && blockLine < afterLine) {
        after = blocks[index];
        afterLine = blockLine;
      }
    }

    if (!before && !after) {
      window.scrollTo(0, ratio * scrollable);
      return;
    }

    // 첫 블록보다 앞이면 문서 처음부터, 마지막 블록 안이면 문서 끝까지를 나눈다
    var scrollY = window.scrollY;
    var startLine = before ? beforeLine : 1;
    var startTop = before ? getRect.call(before).top + scrollY : 0;
    var endLine = after ? afterLine : Math.max(totalLines, line) + 1;
    var endTop = after ? getRect.call(after).top + scrollY : rootElement.scrollHeight;
    var progress = endLine > startLine ? (line - startLine) / (endLine - startLine) : 0;
    window.scrollTo(0, startTop + progress * (endTop - startTop));
  }

  // 화면 맨 위에서 이만큼(px) 안에 든 제목까지를 "지나온 제목"으로 본다 — 제목이 맨 위에 딱 닿기 전에도 그 절을 읽는 중이다
  var READING_OFFSET = 80;
  var HEADING_SELECTOR = 'h1[id],h2[id],h3[id],h4[id],h5[id],h6[id]';

  // 문서의 맨 아래로 본다는 허용 오차(px) — 배율에 따라 스크롤 위치가 소수로 나온다
  var BOTTOM_TOLERANCE = 2;

  // 지금 읽는 곳: "스크롤 위치\n제목 id" (01 F-37, 02 §17.27). 앱이 주기적으로 물어 목차에 표시한다 — 웹 메시지는 꺼져 있다.
  // 지나온 제목 중 마지막이 읽는 절이다. 첫 제목보다 위면 id가 비어 있다.
  // 맨 아래까지 내리면 마지막 제목으로 본다 — 끝의 짧은 절은 제목이 맨 위까지 올라오지 못한다
  function readingPosition() {
    var content = getElementById.call(doc, CONTENT_ID);
    var scrollY = window.scrollY;
    var id = '';
    var headings = content ? querySelectorAll.call(content, HEADING_SELECTOR) : [];
    if (headings.length > 0 && scrollY > 0 && scrollY + window.innerHeight >= rootElement.scrollHeight - BOTTOM_TOLERANCE) {
      id = getAttribute.call(headings[headings.length - 1], 'id');
    } else {
      for (var index = 0; index < headings.length && getRect.call(headings[index]).top <= READING_OFFSET; index++) {
        id = getAttribute.call(headings[index], 'id');
      }
    }

    return Math.round(scrollY) + '\n' + id;
  }

  // 본문에서 term이 나오는 곳을 모두 칠한다. DOM을 바꾸지 않는 방식(CSS Custom Highlight)이라 스크롤·선택·그림을 건드리지 않는다.
  // 글자 조각(텍스트 노드) 하나 안에서만 찾는다 — 굵게 등으로 나뉜 말은 칠하지 못한다
  function highlightText(term, matchCase) {
    if (!window.CSS || !CSS.highlights || typeof Highlight !== 'function') {
      return;
    }

    CSS.highlights.delete(FIND_HIGHLIGHT);
    var content = getElementById.call(doc, CONTENT_ID);
    if (!content || typeof term !== 'string' || term.length === 0) {
      return;
    }

    var needle = matchCase ? term : term.toLowerCase();
    var walker = createTreeWalker.call(doc, content, NodeFilter.SHOW_TEXT);
    var highlight = new Highlight();
    var count = 0;
    for (var node = walker.nextNode(); node && count < FIND_HIGHLIGHT_LIMIT; node = walker.nextNode()) {
      var text = matchCase ? node.data : node.data.toLowerCase();

      // 소문자로 바꾸면 길이가 달라지는 글자가 있다 — 위치가 어긋나므로 그런 조각은 건너뛴다
      if (text.length !== node.data.length) {
        continue;
      }

      for (var at = text.indexOf(needle); at >= 0 && count < FIND_HIGHLIGHT_LIMIT; at = text.indexOf(needle, at + needle.length)) {
        var range = new Range();
        range.setStart(node, at);
        range.setEnd(node, at + needle.length);
        highlight.add(range);
        count++;
      }
    }

    if (count > 0) {
      CSS.highlights.set(FIND_HIGHLIGHT, highlight);
    }
  }

  function renderDiagrams() {
    var mine = ++generation;
    var content = getElementById.call(doc, CONTENT_ID);
    if (!content) {
      return;
    }

    var theme = currentTheme();
    var targets = collectTargets(content);
    var pending = [];

    // 이미 그려 본 그림은 이 호출 안에서 바로 바꾼다 — 화면이 한 번 그려지기 전이라 코드 블록이 잠깐 보였다 사라지지 않는다
    for (var index = 0; index < targets.length; index++) {
      var cached = cache.get(theme + '\n' + targets[index].source);
      if (typeof cached === 'string') {
        show(targets[index].element, targets[index].source, cached);
      } else {
        pending.push(targets[index]);
      }
    }

    if (pending.length > 0) {
      // 불러오기 실패는 여기서 끝낸다 — 코드 블록이 원문 그대로 남는다
      enqueue(function () {
        return renderPending(pending, theme, mine).catch(function () { });
      });
    }
  }

  // 마우스의 뒤로·앞으로 버튼 (01 F-32, 02 §17.22). 문서 영역은 앱 창과 다른 프로세스의 창이라 앱이 이 버튼을 받지 못한다.
  // 웹 메시지는 꺼져 있으므로 링크와 같은 길로 알린다 — 앱이 이 주소로 가는 탐색을 가로채 뒤로·앞으로를 실행한다(주소는 AppConstants와 같아야 한다).
  var HISTORY_URLS = { 3: 'https://appassets.example/history/back', 4: 'https://appassets.example/history/forward' };
  var listen = EventTarget.prototype.addEventListener;

  function onHistoryButton(event) {
    var url = HISTORY_URLS[event.button];
    if (!url) {
      return;
    }

    // 브라우저가 스스로 뒤로·앞으로 가지 않게 막는다
    event.preventDefault();
    if (event.type === 'mouseup') {
      window.location.assign(url);
    }
  }

  listen.call(window, 'mousedown', onHistoryButton, true);
  listen.call(window, 'mouseup', onHistoryButton, true);

  // 본문의 이름 있는 요소(<img name="mdfv">)가 덮지 못하게 바꿀 수 없는 속성으로 둔다
  Object.defineProperty(window, 'mdfv', {
    value: Object.freeze({ renderDiagrams: renderDiagrams, prepareOutput: prepareOutput, isOutputReady: isOutputReady, exportHtml: exportHtml, scrollToLine: scrollToLine, syncToLine: syncToLine, readingPosition: readingPosition, highlightText: highlightText }),
    writable: false,
    configurable: false,
    enumerable: false
  });
})();
