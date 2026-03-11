// JavaScript functions for PostEditModal component
// Import shared utilities to reduce code duplication
import * as TextEditorUtils from '/js/text-editor-utils.js';
import * as EmojiPickerUtils from '/js/emoji-picker-utils.js';

// Re-export shared functions for this component
export const insertTextAtCursor = TextEditorUtils.insertTextAtCursor;
export const replaceTextPreserveCursor = TextEditorUtils.replaceTextPreserveCursor;
export const calculateEmojiPickerPosition = EmojiPickerUtils.calculateEmojiPickerPosition;
