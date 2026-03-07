import { Releaser, Logger } from '@simple-release/core';
import { NpmProject } from '@simple-release/npm';
import { createRequire } from 'module';

const require = createRequire(import.meta.url);
const configPath = require.resolve('./preset-wrapper.js');

async function run() {
  try {
    const logger = new Logger({
      level: 'info'
    });

    const releaser = new Releaser({
      project: new NpmProject(),
      logger
    });

    // Run the release process
    await releaser
      .bump({
        preset: {
          name: configPath
        }
      })
      .commit()
      .tag()
      // .push() // Uncomment to auto-push
      .run();
      
  } catch (err) {
    console.error('Release failed:', err);
    process.exit(1);
  }
}

run();