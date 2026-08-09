; Skopos — Markdown highlighting, inline grammar.
;
; Runs as the second pass over the ranges of the `inline` nodes the block grammar returned.

[
  (code_span)
  (link_title)
] @string

[
  (emphasis_delimiter)
  (code_span_delimiter)
] @punctuation

(emphasis) @emphasis
(strong_emphasis) @strong

; Struck-through text has no category of its own — we give it the muted comment colour.
(strikethrough) @comment

[
  (link_destination)
  (uri_autolink)
] @link

[
  (link_label)
  (link_text)
  (image_description)
] @link

[
  (backslash_escape)
  (hard_line_break)
] @constant

(image [ "!" "[" "]" "(" ")" ] @punctuation)
(inline_link [ "[" "]" "(" ")" ] @punctuation)
(shortcut_link [ "[" "]" ] @punctuation)
