// JavaScript functions for PostCreator component
// Import shared utilities to reduce code duplication
import * as TextEditorUtils from '/js/text-editor-utils.js';
import * as EmojiPickerUtils from '/js/emoji-picker-utils.js';

// Re-export shared functions for this component
export const insertTextAtCursor = TextEditorUtils.insertTextAtCursor;
export const resizeTextarea = TextEditorUtils.resizeTextarea;
export const resetTextarea = TextEditorUtils.resetTextarea;
export const replaceTextPreserveCursor = TextEditorUtils.replaceTextPreserveCursor;
export const blurTextarea = TextEditorUtils.blurTextarea;
export const calculateEmojiPickerPosition = EmojiPickerUtils.calculateEmojiPickerPosition;
