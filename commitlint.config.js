const pkg = require('./package.json');
const types = pkg.config['cz-emoji'].types;

const typeToEmoji = {};
types.forEach(t => {
  typeToEmoji[t.name] = [t.emoji, t.code];
});

module.exports = {
  extends: ['@commitlint/config-conventional'],
  parserPreset: {
    parserOpts: {
      // Capture the leading emoji in a named group 'emoji'
      headerPattern: /^(?<emoji>(?::\w*:|(?:\ud83c[\udf00-\udfff])|(?:\ud83d[\udc00-\ude4f\ude80-\udeff])|[\u2600-\u2B55])\s)(?<type>\w*)(?:\((?<scope>.*)\))?:\s(?<subject>.*)$/,
      headerCorrespondence: ['emoji', 'type', 'scope', 'subject'],
    },
  },
  plugins: [
    {
      rules: {
        'type-emoji-match': (parsed) => {
          const { type, subject, emoji } = parsed;
          if (!type) return [true];

          const allowed = typeToEmoji[type];
          if (!allowed) return [true];

          if (emoji) {
            const emojiTrimmed = emoji.trim();
            const hasPrefixEmoji = allowed.some(e => emojiTrimmed === e);
            if (hasPrefixEmoji) return [true];
          }

          if (subject) {
            const subjectTrimmed = subject.trim();
            const hasSubjectEmoji = allowed.some(e => subjectTrimmed.startsWith(e));
            if (hasSubjectEmoji) return [true];
          }

          return [
            false,
            `Commit for type '${type}' must start with one of its emojis: ${allowed.join(' or ')}`
          ];
        }
      }
    }
  ],
  rules: {
    'subject-empty': [2, 'never'],
    'type-enum': [
      2,
      'always',
      Object.keys(typeToEmoji)
    ],
    'type-emoji-match': [2, 'always']
  }
};
