// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Not exported from index
/** @private */
export class TextMessageFormat {
    static RecordSeparatorCode = 0x1e;
    static RecordSeparator = String.fromCharCode(TextMessageFormat.RecordSeparatorCode);

    static write(output: string): string {
        return `${output}${TextMessageFormat.RecordSeparator}`;
    }

    static parse(input: string): string[] {
        if (input[input.length - 1] !== TextMessageFormat.RecordSeparator) {
            throw new Error("Message is incomplete.");
        }

        const messages = input.split(TextMessageFormat.RecordSeparator);
        messages.pop();
        return messages;
    }
}