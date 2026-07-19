; Skopos — JSON highlighting.
;
; Captures point straight at the highlight categories, so there is no extra mapping
; table between grammar and theme. Colours live only in the theme.
;
; Pattern order matters: on an identical range the earlier one wins, which is why
; the pair key is listed before the generic string.

(pair key: (string) @property)

(string) @string

(number) @number

[
  (true)
  (false)
  (null)
] @constant

(comment) @comment

[
  ","
  ":"
] @punctuation

[
  "{"
  "}"
  "["
  "]"
] @punctuation
