module.exports = {
  extends: ['@commitlint/config-conventional', 'gitmoji'],
  parserPreset: {
    parserOpts: {
      headerPattern: /^(:\w+:)\s(\w+)(?:\(([^)]+)\))?:\s(.+)$/,
      headerCorrespondence: ['gitmoji', 'type', 'scope', 'subject']
    }
  },
  rules: {
    'header-max-length': [2, 'always', 100],
    'type-empty': [2, 'never'],
    'subject-empty': [2, 'never'],
    'subject-case': [2, 'never', ['sentence-case', 'start-case', 'pascal-case', 'upper-case']],
    'type-enum': [
      2,
      'always',
      [
        'feat',
        'fix',
        'docs',
        'style',
        'refactor',
        'perf',
        'test',
        'build',
        'ci',
        'chore',
        'revert',
        'infra'
      ]
    ],
    'start-with-gitmoji': [2, 'always']
  }
};
