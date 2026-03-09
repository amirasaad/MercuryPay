module.exports = {
  extends: ['gitmoji'],
  rules: {
    'subject-empty': [2, 'never'],
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
        'infra',
      ],
    ],
  },
};
