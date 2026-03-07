const czEmojiConfig = require('./node_modules/conventional-changelog-cz-emoji/index.js');

module.exports = czEmojiConfig.then(config => {
  // Override parserOpts to match "emoji type(scope): subject" format
  config.parserOpts.headerPattern = /^(?<emoji>(?::\w*:|(?:\ud83c[\udf00-\udfff])|(?:\ud83d[\udc00-\ude4f\ude80-\udeff])|[\u2600-\u2B55])\s)(?<type>\w*)(?:\((?<scope>.*)\))?:\s(?<subject>.*)$/;
  config.parserOpts.headerCorrespondence = ['emoji', 'type', 'scope', 'subject'];

  // Override writerOpts transform to map text types (feat, fix) to emoji titles
  config.writerOpts.transform = (commit, context) => {
    // Clone commit to avoid modifying immutable object if it is
    const c = { ...commit };
    if (commit.notes) {
        c.notes = commit.notes.map(n => ({ ...n }));
    }

    let discard = true;
    const issues = [];

    c.notes.forEach(note => {
      note.title = 'BREAKING CHANGES';
      discard = false;
    });

    // Map types
    const typeMap = {
      feat: '✨ Features',
      fix: '🐛 Bug Fixes',
      perf: '🚀 Performance Improvements',
      revert: '⏪ Reverts',
      docs: '📝 Documentation',
      style: '💎 Styles',
      refactor: '♻️ Code Refactoring',
      test: '✅ Tests',
      build: '👷 Build System',
      ci: '💚 Continuous Integration',
      chore: '🔨 Chores',
    };

    if (typeMap[c.type]) {
      c.type = typeMap[c.type];
      discard = false;
    } else if (discard) {
      return; // Discard other types
    }

    if (c.scope === '*') {
      c.scope = '';
    }

    if (typeof c.hash === 'string') {
      c.hash = c.hash.substring(0, 7);
    }

    return c;
  };

  return config;
});
