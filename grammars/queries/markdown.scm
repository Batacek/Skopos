; Skopos — Markdown highlighting, block grammar.
;
; This file only sees block structure — headings, lists, code blocks.
; Bold, italics and links arrive from the second pass, see markdown-inline.scm.

(atx_heading) @heading
(setext_heading) @heading

[
  (atx_h1_marker)
  (atx_h2_marker)
  (atx_h3_marker)
  (atx_h4_marker)
  (atx_h5_marker)
  (atx_h6_marker)
  (setext_h1_underline)
  (setext_h2_underline)
] @punctuation

[
  (indented_code_block)
  (fenced_code_block)
  (link_title)
] @string

(fenced_code_block_delimiter) @punctuation
(info_string) @type

(link_destination) @link
(link_label) @link

[
  (list_marker_plus)
  (list_marker_minus)
  (list_marker_star)
  (list_marker_dot)
  (list_marker_parenthesis)
  (thematic_break)
  (block_quote_marker)
] @punctuation

[
  (task_list_marker_checked)
  (task_list_marker_unchecked)
] @constant

(backslash_escape) @constant

; YAML/TOML frontmatter — the grammar has these thanks to EXTENSION_MINUS_METADATA
; a EXTENSION_PLUS_METADATA, viz grammars/README.md.
[
  (minus_metadata)
  (plus_metadata)
] @comment

; GFM tabulky
(pipe_table_header) @strong
(pipe_table_delimiter_row) @punctuation
