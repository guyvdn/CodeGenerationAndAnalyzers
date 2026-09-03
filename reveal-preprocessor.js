const fs = require('fs');
const path = require('path');

const processContent = (markdown) => {
  const lines = markdown.split('\n');
  let inCodeBlock = false;
  
  const processedLines = lines.map(line => {
    // Look for code blocks with titles: ```lang title="My Title"
    const titleMatch = line.match(/^```(\S+)\s+title="([^"]+)"\s*$/);
    if (titleMatch) {
      inCodeBlock = true;
      // Start wrapper, title, then start code block
      return `<div class="code-wrapper"><div class="code-title">${titleMatch[2]}</div>\n\n\`\`\`${titleMatch[1]}`;
    }

    // Detect end of code block
    if (inCodeBlock && line.trim() === '```') {
      inCodeBlock = false;
      return '```\n</div>';
    }

    // Look for lines like <!-- include: filename.md -->
    const match = line.match(/^\s*<!--\s*include:\s*(.+)\s*-->\s*$/);
    if (match) {
      const filePath = match[1].trim();
      try {
        const fullPath = path.resolve(process.cwd(), filePath);
        
        if (fs.existsSync(fullPath)) {
           // Read the file context
           const fileContent = fs.readFileSync(fullPath, 'utf8');
           // Recursively process the included content (to handle titles inside includes)
           const processedContent = processContent(fileContent);

           // Add a separator before the content to ensure it starts on a new slide
           // and a newline after.
          return '\n---\n\n' + processedContent + '\n';
        } else {
           console.warn(`Included file not found: ${filePath}`);
           return `> **Error: Should include ${filePath} but file was not found.**`;
        }
       
      } catch (err) {
        console.error(`Error reading included file ${filePath}:`, err);
        return `> **Error reading ${filePath}**`;
      }
    }
    return line;
  });
  return processedLines.join('\n');
};

module.exports = (markdown, options) => {
  return new Promise((resolve, reject) => {
    try {
      resolve(processContent(markdown));
    } catch (e) {
      reject(e);
    }
  });
};
