import fs from 'node:fs/promises';
import pathModule from 'node:path';
import { Parser } from '@asyncapi/parser';

const paths = process.argv.slice(2);
if (paths.length === 0) {
  console.error('At least one AsyncAPI document path is required.');
  process.exit(2);
}

const parser = new Parser();
let failed = false;
for (const path of paths) {
  const source = await fs.readFile(path, 'utf8');
  const { document, diagnostics } = await parser.parse(source, { source: pathModule.resolve(path) });
  const errors = diagnostics.filter((diagnostic) => diagnostic.severity === 0);
  if (!document || errors.length > 0) {
    failed = true;
    console.error(`${path}: invalid AsyncAPI document`);
    for (const error of errors) console.error(`- ${error.message}`);
  } else {
    console.log(`${path}: valid AsyncAPI ${document.version()}`);
  }
}
process.exit(failed ? 1 : 0);
