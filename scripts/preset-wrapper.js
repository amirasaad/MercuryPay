const configPromise = require('../changelog.config.js');

// Simple-release (via conventional-changelog-preset-loader) expects a function
// that returns the config (or a promise resolving to it).
module.exports = function() {
  return configPromise.then(config => {
    // Add whatBump function directly to the config object
    // conventional-recommended-bump expects whatBump at the root
    config.whatBump = (commits) => {
      let level = 2; // Default to patch
      let breakings = 0;
      let features = 0;
      
      commits.forEach(commit => {
        if (commit.notes.length > 0) {
          breakings += commit.notes.length;
          level = 0; // Major
        } else if (commit.type === 'feat') {
          features += 1;
          if (level === 2) {
            level = 1; // Minor
          }
        }
      });
      
      return {
        level: level,
        releaseType: level === 0 ? 'major' : level === 1 ? 'minor' : 'patch',
        reason: `There are ${breakings} BREAKING CHANGES and ${features} features`
      };
    };
    
    // Also map parserOpts to parser if needed, or conventional-recommended-bump might use parserOpts?
    // Let's assume it might use parserOpts if parser is missing, or we map it to be safe.
    if (config.parserOpts) {
      config.parser = config.parserOpts;
    }
    
    // Map writerOpts to writer for simple-release
    if (config.writerOpts) {
      config.writer = config.writerOpts;
    }

    return config;
  });
};